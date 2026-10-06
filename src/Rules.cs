using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using NBig=System.Numerics.BigInteger;
namespace Astra {
sealed class Rules {
 public string Chain="eth",Prefix="",Suffix="",CharChoice="all",Characters="";
 public bool Sensitive=false; public int Repeat=0;
 public bool Tron{get{return Chain=="tron";}}
 public int Length{get{return Tron?33:40;}}
 public string Label{get{return Chain=="polygon"?"Polygon PoS Mainnet (chainId=137)":Tron?"TRON Mainnet":"Ethereum Mainnet (chainId=1)";}}
 public double Probability;
 public string Alphabet{get{return Tron?Crypto.Base58:"0123456789abcdefABCDEF";}}
 static char Fold(char c){return char.ToLowerInvariant(c);}
 bool Same(char a,char b){return Sensitive?a==b:Fold(a)==Fold(b);}
 public void Validate(){
  Chain=Chain.ToLowerInvariant();if(Chain=="ethereum")Chain="eth";
  if(Chain!="eth"&&Chain!="tron"&&Chain!="polygon")throw new ArgumentException("Network must be tron, eth or polygon.");
  if(Prefix.Length>Length||Suffix.Length>Length||Repeat<0||Repeat>Length)throw new ArgumentException("Prefix or suffix exceeds the address length.");
  if(Repeat>0&&Suffix.Length>0)throw new ArgumentException("Fixed suffix and repeated suffix are mutually exclusive.");
  foreach(char c in Prefix+Suffix)if(!Alphabet.Contains(c))throw new ArgumentException("Invalid prefix/suffix character: "+c+". Omit T / 0x from the prefix.");
  string choice=CharChoice;bool preset=true;
  switch(choice){case "all":choice=Alphabet;break;case "digits":choice="0123456789";break;case "letters":choice="abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";break;case "lower":choice="abcdefghijklmnopqrstuvwxyz";break;case "upper":choice="ABCDEFGHIJKLMNOPQRSTUVWXYZ";break;default:preset=false;break;}
  if(!preset)foreach(char c in choice)if(!Alphabet.Contains(c))throw new ArgumentException("Invalid character in custom set: "+c);
  Characters=new string(choice.Where(c=>Alphabet.Contains(c)).Distinct().ToArray());
  if(Repeat>0&&Characters.Length==0)throw new ArgumentException("The repeated-suffix character set is empty.");
  Probability=CalculateProbability();if(Probability<=0)throw new ArgumentException("Prefix and suffix conflict, or this rule is unreachable on the selected network.");
  // A fully fixed EVM payload determines its checksum, so reject impossible casing.
  if(!Tron&&Sensitive&&Repeat==0){char[] fixedChars=new char[40];for(int i=0;i<Prefix.Length;i++)fixedChars[i]=Prefix[i];for(int i=0;i<Suffix.Length;i++)fixedChars[40-Suffix.Length+i]=Suffix[i];if(fixedChars.All(c=>c!=0)&&!Match(Crypto.Checksum(new string(fixedChars))))throw new ArgumentException("Full-address condition does not match actual EIP-55 checksum case.");}
 }
 public bool Match(string address){string s=address.Substring(Tron?1:2);if(s.Length!=Length)return false;for(int i=0;i<Prefix.Length;i++)if(!Same(Prefix[i],s[i]))return false;for(int i=0;i<Suffix.Length;i++)if(!Same(Suffix[i],s[s.Length-Suffix.Length+i]))return false;if(Repeat>0){char last=s[s.Length-1];if(!Characters.Any(c=>Same(c,last)))return false;for(int i=1;i<Repeat;i++)if(!Same(last,s[s.Length-1-i]))return false;}return true;}
 public byte[] Pack(){byte[] b=new byte[168];int[] n={Tron?1:0,Sensitive?1:0,Prefix.Length,Suffix.Length,Repeat,Characters.Length};for(int i=0;i<6;i++)Buffer.BlockCopy(BitConverter.GetBytes(n[i]),0,b,i*4,4);Encoding.ASCII.GetBytes(Prefix).CopyTo(b,24);Encoding.ASCII.GetBytes(Suffix).CopyTo(b,65);Encoding.ASCII.GetBytes(Characters).CopyTo(b,106);return b;}
 List<HashSet<char>[]> Constraints(){
  var targets=Repeat>0?Characters.Select(c=>Sensitive?c:Fold(c)).Distinct().ToArray():new char[]{'\0'};
  var result=new List<HashSet<char>[]>();foreach(char target in targets){var sets=new HashSet<char>[Length];for(int i=0;i<Length;i++)sets[i]=new HashSet<char>(Alphabet);for(int i=0;i<Prefix.Length;i++)sets[i].RemoveWhere(c=>!Same(c,Prefix[i]));for(int i=0;i<Suffix.Length;i++){int j=i;sets[Length-Suffix.Length+i].RemoveWhere(c=>!Same(c,Suffix[j]));}if(Repeat>0)for(int i=Length-Repeat;i<Length;i++)sets[i].RemoveWhere(c=>!Same(c,target));result.Add(sets);}return result;
 }
 double CalculateProbability(){double total=0;foreach(var sets in Constraints()){if(sets.Any(s=>s.Count==0))continue;if(!Tron){if(sets.All(s=>s.Select(Fold).Distinct().Count()==1)){string fixedAddress=Crypto.Checksum(new string(sets.Select(s=>Fold(s.First())).ToArray())).Substring(2);if(Enumerable.Range(0,40).All(i=>sets[i].Contains(fixedAddress[i])))total+=Math.Pow(16,-40);continue;}double p=1;foreach(var s in sets){double q=0;foreach(char c in s)q+=char.IsDigit(c)?1.0/16:1.0/32;p*=q;}total+=p;}else{
   if(sets.All(s=>s.Count==1)){if(Crypto.ValidTron("T"+new string(sets.Select(s=>s.First()).ToArray())))total+=Math.Pow(2,-160);continue;}
   // 0x41 || 160-bit payload || 32-bit checksum, modeled as uniform 192 bits.
   // Bounded base58 digit DP accounts for TRON's nonuniform leading characters.
   NBig lo=(NBig)65<<192,hi=((NBig)66<<192)-1;NBig count=CountBound(hi,sets)-CountBound(lo-1,sets);total+=(double)count/Math.Pow(2,192);
  }}return Math.Min(total,1);
 }
 static NBig CountBound(NBig bound,HashSet<char>[] sets){int[] digits=new int[34];for(int i=33;i>=0;i--){digits[i]=(int)(bound%58);bound/=58;}NBig tight=1,loose=0;for(int i=0;i<34;i++){var allowed=i==0?new HashSet<char>(new[]{'T'}):sets[i-1];NBig nextLoose=loose*allowed.Count,nextTight=0;foreach(char c in allowed){int d=Crypto.Base58.IndexOf(c);if(d<digits[i])nextLoose+=tight;else if(d==digits[i])nextTight+=tight;}loose=nextLoose;tight=nextTight;}return loose+tight;}
 public string Description{get{return Label+" | prefix="+Prefix+" | "+(Repeat>0?"repeat="+Repeat+" characters="+Characters:"suffix="+Suffix)+" | "+(Sensitive?"Match case":"case-insensitive");}}
}
}
