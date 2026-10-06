using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace Astra {
static class ExportTests {
 public static void Run(Action<string> log){
  string root=Path.GetFullPath(Path.Combine("test-output","export-"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)));Directory.CreateDirectory(root);
  foreach(string chain in new[]{"eth","tron","polygon"}){
   string folder=Path.Combine(root,chain);Directory.CreateDirectory(folder);var o=new Options{Rule=new Rules{Chain=chain,Repeat=1},Count=7,OutputDirectory=folder,Format="both",Groups=16,Batch=4};
   var snap=Engine.Run(o,CancellationToken.None,s=>{},s=>{});Crypto.Check(snap.Saved==7,"both count");Crypto.Check(Directory.GetFiles(folder,"*.txt").Length==1&&Directory.GetFiles(folder,"*.xlsx").Length==1,"both outputs");Crypto.Check(Directory.GetFiles(folder,"*.pending").Length==0,"journal cleanup");
   var acl=File.GetAccessControl(Directory.GetFiles(folder,"*.xlsx")[0]);Crypto.Check(acl.AreAccessRulesProtected,"XLSX protected DACL");
   log(chain+" folder / TXT+XLSX / count / permissions PASS");
  }
  string excelOnly=Path.Combine(root,"excel-only");Directory.CreateDirectory(excelOnly);var only=new Options{Rule=new Rules{Chain="polygon",Repeat=1},Count=3,Format="xlsx",OutputDirectory=excelOnly,Groups=16,Batch=4};Engine.Run(only,CancellationToken.None,s=>{},s=>{});Crypto.Check(Directory.GetFiles(excelOnly,"*.txt").Length==0&&Directory.GetFiles(excelOnly,"*.xlsx").Length==1,"XLSX-only mode");
  string appended=Path.Combine(root,"append");Directory.CreateDirectory(appended);var a=new Options{Rule=new Rules{Chain="tron",Repeat=1},Count=2,Format="txt",OutputDirectory=appended,Append=true,Groups=16,Batch=4};Engine.Run(a,CancellationToken.None,s=>{},s=>{});Engine.Run(a,CancellationToken.None,s=>{},s=>{});Crypto.Check(File.ReadAllLines(Path.Combine(appended,"tron_results.txt")).Count(l=>l.Length>0&&!l.StartsWith("#"))==4,"folder append");
  // Public, known private scalars: suitable for visual checks; never fund these addresses.
  string vectors=Path.Combine(root,"public-vectors");Directory.CreateDirectory(vectors);using(var excel=new ExcelExport(Path.Combine(vectors,"public_TEST_ONLY"),"Polygon PoS Mainnet (chainId=137)",3)){
   for(int i=1;i<=7;i++){byte[] key=new byte[32];key[31]=(byte)i;try{excel.Add(new Wallet{Key=key,Address=Crypto.Address(Crypto.Public(key),false)});}finally{Crypto.Wipe(key);}}
   excel.Finish();Crypto.Check(excel.Published==7&&excel.Parts==3,"XLSX row limit splits 3+3+1");
  }
  string collision=Path.Combine(root,"collision");Directory.CreateDirectory(collision);string stem=Path.Combine(collision,"test");File.WriteAllText(stem+"_0001.xlsx","sentinel");bool rejected=false;
  using(var excel=new ExcelExport(stem,"TEST")){byte[] key=new byte[32];key[31]=1;try{excel.Add(new Wallet{Key=key,Address=Crypto.Address(Crypto.Public(key),false)});try{excel.Finish();}catch(IOException){rejected=true;}}finally{Crypto.Wipe(key);}}
  Crypto.Check(rejected&&File.ReadAllText(stem+"_0001.xlsx")=="sentinel"&&File.Exists(stem+"_0001.xlsx.pending"),"publish failure keeps journal, prevents overwrite");
  using(var cts=new CancellationTokenSource()){string folder=Path.Combine(root,"cancel");Directory.CreateDirectory(folder);var o=new Options{Rule=new Rules{Chain="eth",Repeat=1},Count=0,Seconds=20,OutputDirectory=folder,Format="both",Groups=16,Batch=4};cts.CancelAfter(4500);var snap=Engine.Run(o,cts.Token,s=>{},s=>{});Crypto.Check(snap.Saved==snap.Accepted&&Directory.GetFiles(folder,"*.pending").Length==0,"cancel drains XLSX");}
  log("Excel-only / volumes / leading-zero vectors / append / cancel / failure retention PASS");log("EXPORT_TEST_DIR="+root);
 }
}
}
