using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using ECPoint=Org.BouncyCastle.Math.EC.ECPoint;
using Org.BouncyCastle.Crypto.Digests;

namespace Astra {
static class Crypto {
 public static readonly Org.BouncyCastle.Asn1.X9.X9ECParameters Curve=SecNamedCurves.GetByName("secp256k1");
 public static readonly BigInteger Order=Curve.N;
 public const string Base58="123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
 public static byte[] RandomScalar(){byte[] b=new byte[32];using(var rng=RandomNumberGenerator.Create()){do{rng.GetBytes(b);}while(new BigInteger(1,b).SignValue==0||new BigInteger(1,b).CompareTo(Order)>=0);}return b;}
 public static byte[] ScalarAt(byte[] seed,ulong offset){var n=new BigInteger(1,seed).Add(new BigInteger(offset.ToString())).Mod(Order);if(n.SignValue==0)throw new InvalidOperationException("Invalid zero private key; refusing output.");return Pad(n.ToByteArrayUnsigned(),32);}
 public static byte[] Pad(byte[] b,int n){byte[] r=new byte[n];Buffer.BlockCopy(b,0,r,n-b.Length,b.Length);return r;}
 public static ECPoint Public(byte[] key){var k=new BigInteger(1,key);if(k.SignValue==0||k.CompareTo(Order)>=0)throw new ArgumentException("Private key is outside the secp256k1 range");return Curve.G.Multiply(k).Normalize();}
 public static byte[] Keccak(byte[] bytes){var d=new KeccakDigest(256);d.BlockUpdate(bytes,0,bytes.Length);byte[] r=new byte[32];d.DoFinal(r,0);return r;}
 public static byte[] Hash(byte[] bytes){using(var d=SHA256.Create())return d.ComputeHash(bytes);}
 public static string Hex(byte[] bytes){var b=new StringBuilder(bytes.Length*2);foreach(byte x in bytes)b.Append(x.ToString("x2"));return b.ToString();}
 public static byte[] Unhex(string s){if(s.Length%2!=0)throw new ArgumentException("Hex length");byte[] b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;}
 public static byte[] PubBytes(ECPoint point){return point.Normalize().GetEncoded(false).Skip(1).ToArray();}
 public static string Checksum(string lower){lower=lower.ToLowerInvariant();var hash=Hex(Keccak(Encoding.ASCII.GetBytes(lower)));char[] s=lower.ToCharArray();for(int i=0;i<40;i++)if(s[i]>='a'&&Convert.ToInt32(hash[i].ToString(),16)>=8)s[i]=char.ToUpperInvariant(s[i]);return "0x"+new string(s);}
 public static string Address(ECPoint point,bool tron){byte[] raw=Keccak(PubBytes(point)).Skip(12).ToArray();if(!tron)return Checksum(Hex(raw));byte[] b=new byte[25];b[0]=0x41;Buffer.BlockCopy(raw,0,b,1,20);Buffer.BlockCopy(Hash(Hash(b.Take(21).ToArray())),0,b,21,4);var n=new BigInteger(1,b);string s="";var radix=BigInteger.ValueOf(58);while(n.SignValue>0){var qr=n.DivideAndRemainder(radix);s=Base58[qr[1].IntValue]+s;n=qr[0];}return s;}
 public static byte[] PackPoint(ECPoint p){byte[] r=new byte[68];if(p.IsInfinity){r[64]=1;return r;}byte[] b=PubBytes(p);for(int k=0;k<2;k++)for(int i=0;i<32;i++)r[k*32+i]=b[k*32+31-i];return r;}
 public static bool ValidTron(string address){if(address.Length!=34||address[0]!='T')return false;BigInteger n=BigInteger.Zero;foreach(char c in address){int v=Base58.IndexOf(c);if(v<0)return false;n=n.Multiply(BigInteger.ValueOf(58)).Add(BigInteger.ValueOf(v));}byte[] b=n.ToByteArrayUnsigned();if(b.Length!=25||b[0]!=0x41)return false;byte[] h=Hash(Hash(b.Take(21).ToArray()));return Enumerable.Range(0,4).All(i=>h[i]==b[i+21]);}
 public static void Wipe(byte[] b){if(b!=null)Array.Clear(b,0,b.Length);}
 public static void KnownTests(){
  byte[] key=new byte[32];key[31]=1;var p=Public(key);
  Check(Address(p,false)=="0x7E5F4552091A69125d5DfCb7b8C2659029395Bdf","ETH key 1");
  Check(Address(p,true)=="TMVQGm1qAQYVdetCeGRRkTWYYrLXuHK2HC","TRON key 1");
  Check(Hex(Keccak(new byte[0]))=="c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470","Keccak empty");
  string[] vectors={"52908400098527886E0F7030069857D2E4169EE7","8617E340B3D01FA5F11F306F4090FD50E238070D","de709f2102306220921060314715629080e2fb77","27b1fdb04752bbc536007a920d24acb045561c26","5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed","fB6916095ca1df60bB79Ce92cE3Ea74c37c5d359","dbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB"};
  foreach(string v in vectors)Check(Checksum(v)=="0x"+v,"EIP-55 "+v);
 }
 public static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("Self-test failed: "+label);}
}
}

