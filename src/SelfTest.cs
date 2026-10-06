using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Org.BouncyCastle.Math;
namespace Astra {
static class SelfTest {
 static void ExpectError(Action a,string name){try{a();}catch(ArgumentException){return;}throw new Exception("Invalid input was not rejected: "+name);}
 public static void RulesTests(){
  var r=new Rules{Chain="eth",Repeat=8};r.Validate();Crypto.Check(Math.Abs(r.Probability-Math.Pow(16,-7))<1e-16,"EVM repeat probability");
  var p=new Rules{Chain="polygon",Repeat=8};p.Validate();Crypto.Check(r.Probability==p.Probability,"polygon shared difficulty");
  r=new Rules{Chain="eth",Repeat=8,Sensitive=true};r.Validate();Crypto.Check(Math.Abs(r.Probability-(10*Math.Pow(16,-8)+12*Math.Pow(32,-8)))<1e-16,"strict repeat probability");
  r=new Rules{Repeat=8,CharChoice="aA"};r.Validate();Crypto.Check(r.Match("0x"+new string('1',32)+"aAaAaAaA"),"fold repeated case");Crypto.Check(Math.Abs(r.Probability-Math.Pow(16,-8))<1e-16,"dedup folded targets");r.Sensitive=true;r.Validate();Crypto.Check(!r.Match("0x"+new string('1',32)+"aAaAaAaA"),"strict repeated case");
  r=new Rules{Chain="tron",Repeat=8,CharChoice="digits"};r.Validate();Crypto.Check(r.Characters=="123456789","TRON filtering");
  ExpectError(()=>new Rules{Chain="tron",Repeat=8,CharChoice="0"}.Validate(),"TRON 0");ExpectError(()=>new Rules{Repeat=8,CharChoice="g"}.Validate(),"EVM g");ExpectError(()=>new Rules{Repeat=8,Suffix="1"}.Validate(),"mutual exclusion");
  ExpectError(()=>new Rules{Prefix=new string('1',40),Suffix="2"}.Validate(),"overlap conflict");ExpectError(()=>new Rules{Chain="tron",Prefix="z",Sensitive=true}.Validate(),"unreachable TRON prefix");
  r=new Rules{Prefix=new string('1',40),Suffix="1"};r.Validate();Crypto.Check(Math.Abs(r.Probability-Math.Pow(16,-40))<1e-55,"overlap counted once");
  r=new Rules{Repeat=8,Prefix=new string('1',34)};r.Validate();Crypto.Check(r.Match("0x"+new string('1',40)),"9 or more repeat accepted");
  ExpectError(()=>new Rules{Chain="tron",Prefix="MVQGm1qAQYVdetCeGRRkTWYYrLXuHK2HD",Sensitive=true}.Validate(),"fully fixed bad Base58 checksum");
  r=new Rules{Chain="tron",Prefix="MVQGm1qAQYVdetCeGRRkTWYYrLXuHK2HC",Sensitive=true};r.Validate();Crypto.Check(r.Probability==Math.Pow(2,-160),"full TRON valid payload probability");
  r=new Rules{Prefix="7E5F4552091A69125d5DfCb7b8C2659029395Bdf",Sensitive=true};r.Validate();Crypto.Check(r.Probability==Math.Pow(16,-40),"fully fixed EIP55 probability");
  for(int i=0;i<128;i++){byte[] s=Crypto.RandomScalar();Crypto.Check(new BigInteger(1,s).SignValue>0&&new BigInteger(1,s).CompareTo(Crypto.Order)<0,"random scalar range");Crypto.Wipe(s);}
 }
 public static void Gpu(Cuda gpu,bool extended){
  int n=extended?256:32;byte[] points=new byte[n*68];var ps=new Org.BouncyCastle.Math.EC.ECPoint[n];
  // Deterministic public vectors NEVER enter the production random-seed path.
  for(int i=0;i<n;i++){BigInteger key=i==0?BigInteger.One:i==1?Crypto.Order.Subtract(BigInteger.One):new BigInteger(1,Crypto.Hash(Encoding.ASCII.GetBytes("Astra public audit vector "+i))).Mod(Crypto.Order.Subtract(BigInteger.One)).Add(BigInteger.One);ps[i]=Crypto.Curve.G.Multiply(key).Normalize();Crypto.PackPoint(ps[i]).CopyTo(points,i*68);}
  ulong input=gpu.Alloc(points.Length),output=gpu.Alloc(n*Engine.HitSize);try{
   var rules=new List<Rules>{new Rules(),new Rules{Chain="polygon"},new Rules{Chain="tron"},new Rules{Prefix="a",Suffix="1"},new Rules{Repeat=2},new Rules{Chain="tron",Repeat=2,Sensitive=true}};
   if(extended)foreach(string chain in new[]{"eth","polygon","tron"})foreach(bool sensitive in new[]{false,true}){
    rules.Add(new Rules{Chain=chain,Sensitive=sensitive,Repeat=1,CharChoice="digits"});rules.Add(new Rules{Chain=chain,Sensitive=sensitive,Repeat=1,CharChoice="upper"});rules.Add(new Rules{Chain=chain,Sensitive=sensitive,Repeat=2,CharChoice="letters"});
    string a=Crypto.Address(ps[3],chain=="tron").Substring(chain=="tron"?1:2);rules.Add(new Rules{Chain=chain,Sensitive=sensitive,Prefix=a.Substring(0,2),Suffix=a.Substring(a.Length-2)});
   }
   foreach(Rules rule in rules){rule.Validate();gpu.Constant("rule",rule.Pack());gpu.Put(input,points);gpu.Clear(output);gpu.Launch("audit",(n+127)/128,128,input,output,n);byte[] data=gpu.Get(output,n*Engine.HitSize);CheckAll(data,ps,rule,"all-candidate address/match");Crypto.Wipe(data);
    gpu.Launch("walkAudit",(n+127)/128,128,input,output,n);data=gpu.Get(output,n*Engine.HitSize);var shifted=ps.Select(p=>p.Add(Crypto.Curve.G.Multiply(BigInteger.ValueOf(128))).Normalize()).ToArray();CheckAll(data,shifted,rule,"field add/mul/inverse walk");Crypto.Wipe(data);
   }
   // Test batch-inversion search, all offsets, and seed+offset correspondence.
   ulong states=gpu.Alloc(128*68),bases=gpu.Alloc(68),reset=gpu.Alloc(4),hits=gpu.Alloc(Engine.HitSize);try{
    for(int batch=1;batch<=(extended?64:32);batch*=2){gpu.Put(bases,Crypto.PackPoint(ps[2]));gpu.Put(reset,new byte[]{1,0,0,0});gpu.Launch("initialize",1,128,states,bases,reset);var rule=new Rules{Chain="polygon"};rule.Validate();gpu.Constant("rule",rule.Pack());
     for(int iteration=0;iteration<2;iteration++){gpu.Clear(hits);gpu.Launch("search",1,128,states,hits,(ulong)(iteration*batch*128),batch);var h=gpu.Get(hits,Engine.HitSize);Crypto.Check(Engine.Found(h,0),"search all matches");ulong at=BitConverter.ToUInt64(h,0);var p=ps[2].Add(Crypto.Curve.G.Multiply(new BigInteger(at.ToString()))).Normalize();Crypto.Check(Engine.HitAddress(h,0)==Crypto.Address(p,false),"search offset");for(int j=0;j<64;j++)Crypto.Check(h[8+j]==Crypto.PubBytes(p)[j],"search public key");Crypto.Wipe(h);}
     var stateBytes=gpu.Get(states,128*68);for(int lane=0;lane<128;lane++){var expected=Crypto.PackPoint(ps[2].Add(Crypto.Curve.G.Multiply(BigInteger.ValueOf(2*batch*128+lane+1))));for(int k=0;k<68;k++)Crypto.Check(stateBytes[lane*68+k]==expected[k],"batch state / no skipped candidates");}Crypto.Wipe(stateBytes);
    }
    if(extended){int batch=32;var allPoints=new Org.BouncyCastle.Math.EC.ECPoint[128*batch];var p=ps[4].Add(Crypto.Curve.G);for(int i=0;i<allPoints.Length;i++){allPoints[i]=p.Normalize();p=p.Add(Crypto.Curve.G);}ulong all=gpu.Alloc(allPoints.Length*Engine.HitSize);try{
     foreach(string chain in new[]{"eth","polygon","tron"})foreach(bool sensitive in new[]{false,true})foreach(int kind in new[]{0,1,2,3}){
      string target=Crypto.Address(allPoints[33],chain=="tron").Substring(chain=="tron"?1:2);
      var rule=new Rules{Chain=chain,Sensitive=sensitive,Prefix=kind==0?target.Substring(0,1):"",Suffix=kind==0?target.Substring(target.Length-1):"",Repeat=kind==0?0:kind==1?2:1,CharChoice=kind==3?"58aB":"all"};rule.Validate();gpu.Constant("rule",rule.Pack());gpu.Put(bases,Crypto.PackPoint(ps[4]));gpu.Put(reset,new byte[]{1,0,0,0});gpu.Launch("initialize",1,128,states,bases,reset);gpu.Clear(hits);gpu.Clear(all);gpu.Launch("searchAudit",1,128,states,hits,(ulong)0,batch,all);var audit=gpu.Get(all,allPoints.Length*Engine.HitSize);CheckAll(audit,allPoints,rule,"production batch all-candidate / prefilter false negatives");for(int i=0;i<allPoints.Length;i++)Crypto.Check(BitConverter.ToUInt64(audit,i*Engine.HitSize)==(ulong)i+1,"candidate enumeration");Crypto.Wipe(audit);
     }
    }finally{gpu.Free(all);}}
   }finally{gpu.Free(states);gpu.Free(bases);gpu.Free(reset);gpu.Free(hits);}
  }finally{gpu.Free(input);gpu.Free(output);Crypto.Wipe(points);}
 }
 static void CheckAll(byte[] data,Org.BouncyCastle.Math.EC.ECPoint[] ps,Rules rule,string label){for(int i=0;i<ps.Length;i++){string cpu=Crypto.Address(ps[i],rule.Tron);Crypto.Check(Engine.HitAddress(data,i)==cpu,label+" address "+rule.Chain+" #"+i);Crypto.Check(Engine.Found(data,i)==rule.Match(cpu),label+" match/false-negative "+rule.Chain+" #"+i);byte[] pub=Crypto.PubBytes(ps[i]);for(int j=0;j<64;j++)Crypto.Check(data[i*Engine.HitSize+8+j]==pub[j],label+" pub");}}
 public static void Run(bool extended,Action<string> log){var sw=Stopwatch.StartNew();Crypto.KnownTests();RulesTests();log("CPU known vectors, EIP-55, rules and difficulty PASS");using(var gpu=new Cuda()){Gpu(gpu,extended);log("CUDA all-candidate cross-check PASS | "+gpu.Device+" | driver="+gpu.DriverVersion+" | sm="+gpu.Major+gpu.Minor);}log("All self-tests passed in "+sw.Elapsed.TotalSeconds.ToString("F2")+" seconds.");}
}
}

