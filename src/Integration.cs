using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace Astra {
static class Integration {
 public static void Run(Action<string> log){
  string folder=Path.GetFullPath(Path.Combine("test-output",DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)));Directory.CreateDirectory(folder);var keys=new HashSet<string>();
  foreach(string chain in new[]{"eth","polygon","tron"}){
   string file=Path.Combine(folder,chain+".txt");var o=new Options{Rule=new Rules{Chain=chain,Repeat=1,CharChoice="digits"},Count=12,Output=file,Groups=16,Batch=4};var s=Engine.Run(o,CancellationToken.None,x=>{},x=>{});Crypto.Check(s.Saved==12,"count limit "+chain);
   o.Append=true;o.Count=3;s=Engine.Run(o,CancellationToken.None,x=>{},x=>{});Crypto.Check(s.Saved==3,"append count");int lines=0;foreach(string line in File.ReadAllLines(file)){if(line.StartsWith("#")||line.Trim().Length==0)continue;var parts=line.Split('\t');Crypto.Check(parts.Length==3,"record format");Crypto.Check(parts[0]==o.Rule.Label,"network label");Crypto.Check(parts[2].Length==64,"private key length");var k=Crypto.Unhex(parts[2]);Crypto.Check(Crypto.Address(Crypto.Public(k),o.Rule.Tron)==parts[1],"saved key-address");Crypto.Check(o.Rule.Match(parts[1]),"saved match");Crypto.Check(keys.Add(parts[2]),"independent run key uniqueness");Crypto.Wipe(k);lines++;}Crypto.Check(lines==15,"append preservation");
   bool blocked=false;try{using(var writer=new SecureWriter(file,false,o.Rule.Label,chain)){}}catch(IOException){blocked=true;}Crypto.Check(blocked,"prevent overwrite");
   log(chain+" count/labels/append/no-overwrite/independent key-address verification PASS");
   var fixedRule=new Rules{Chain=chain,Sensitive=true,Prefix=chain=="tron"?"M":"a",Suffix=chain=="tron"?"8":"B"};string fixedFile=Path.Combine(folder,chain+"-fixed.txt");var fixedOptions=new Options{Rule=fixedRule,Count=2,Output=fixedFile,Groups=16,Batch=4};var fixedSnap=Engine.Run(fixedOptions,CancellationToken.None,x=>{},x=>{});Crypto.Check(fixedSnap.Saved==2,"fixed prefix and suffix count");foreach(string line in File.ReadAllLines(fixedFile)){if(line.StartsWith("#")||line.Trim().Length==0)continue;var parts=line.Split('\t');Crypto.Check(fixedRule.Match(parts[1]),"strict fixed prefix/suffix");Crypto.Check(keys.Add(parts[2]),"fixed task independent key");}log(chain+" case-sensitive fixed prefix AND suffix PASS");
  }
  var scalars=keys.Select(k=>new Org.BouncyCastle.Math.BigInteger(1,Crypto.Unhex(k))).OrderBy(k=>k).ToArray();var gap=Org.BouncyCastle.Math.BigInteger.One.ShiftLeft(40);for(int i=1;i<scalars.Length;i++)Crypto.Check(scalars[i].Subtract(scalars[i-1]).CompareTo(gap)>0,"delivered keys from independent groups, not a shared offset sequence");Crypto.Check(scalars[0].Add(Crypto.Order).Subtract(scalars[scalars.Length-1]).CompareTo(gap)>0,"modular wrap independence");log("No duplicate or adjacent scalars within 2^40 across tasks/networks/append PASS (sample check, not proof of randomness)");
  string timed=Path.Combine(folder,"timed.txt");var timedOptions=new Options{Rule=new Rules{Chain="polygon",Suffix="123456789abcdef12345"},Count=0,Seconds=4,Output=timed,Groups=16,Batch=8,ReseedSeconds=1};var timedSnap=Engine.Run(timedOptions,CancellationToken.None,s=>{},x=>{});Crypto.Check(timedSnap.Elapsed>=4&&timedSnap.Elapsed<15,"time limit");Crypto.Check(timedSnap.Reseeds>=32,"continuous reseeding");log("Time limit / periodic reseeding PASS");
  using(var cts=new CancellationTokenSource()){cts.CancelAfter(4000);var opts=new Options{Rule=new Rules{Chain="polygon",Repeat=1},Count=0,Output=Path.Combine(folder,"cancel.txt"),Groups=16,Batch=4};var snap=Engine.Run(opts,cts.Token,x=>{},x=>{});Crypto.Check(snap.Saved==snap.Accepted,"cancellation drain");log("Cancellation / background writing / flush PASS; saved "+snap.Saved);}
  bool writeFailed=false;using(var writer=new SecureWriter(Path.Combine(folder,"write-failure.txt"),false,"TEST","test")){var field=typeof(SecureWriter).GetField("stream",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);((FileStream)field.GetValue(writer)).Dispose();try{writer.Finish();}catch(IOException){writeFailed=true;}}Crypto.Check(writeFailed,"writer failure propagates");log("Injected background writer failure (closed stream) reported PASS");
  log("All integration tests passed. Test wallets are for verification only; do not fund them. Test files: "+folder);
 }
}
}
