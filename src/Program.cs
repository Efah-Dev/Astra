using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace Astra {
static class Program {
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(IntPtr handler,bool add);
 [STAThread] static int Main(string[] args){
#if !WINDOWS_GUI
  Console.OutputEncoding=new UTF8Encoding(false);Console.InputEncoding=Encoding.UTF8;
  SetConsoleCtrlHandler(IntPtr.Zero,false);
#endif
  try{
#if WINDOWS_GUI
   Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
   return Gui.Run();
#else
   if(args.Contains("--gui"))return Gui.Run();
   if(args.Contains("--help")||args.Contains("-h")){Help();return 0;}
   if(args.Contains("--compile")){File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"kernel-sm120.ptx"),Cuda.Compile(12,0));Console.WriteLine("CUDA sm120 PTX build complete");return 0;}
   if(args.Contains("--self-test")){SelfTest.Run(args.Contains("--extended"),Console.WriteLine);return 0;}
   if(args.Contains("--integration-test")){Integration.Run(Console.WriteLine);return 0;}
   if(args.Contains("--export-test")){ExportTests.Run(Console.WriteLine);return 0;}
   if(args.Length==0){Console.WriteLine("Astra v1.1.0 · CUDA address generator\n1. Open graphical interface\n2. Interactive console generation\n3. Extended self-test\n0. Exit");string selection=Console.ReadLine();if(selection=="1")return Gui.Run();if(selection=="3"){SelfTest.Run(true,Console.WriteLine);return 0;}if(selection!="2")return 0;return Search(Menu());}
   return Search(Parse(args));
#endif
  }catch(Exception ex){Console.Error.WriteLine("Error: "+ex.Message);if(Environment.GetEnvironmentVariable("ASTRA_DIAGNOSTICS")=="1")Console.Error.WriteLine(ex.StackTrace);return 1;}
 }
 static int Search(Options o){using(var cts=new CancellationTokenSource()){ConsoleCancelEventHandler cancel=(s,e)=>{e.Cancel=true;cts.Cancel();};Console.CancelKeyPress+=cancel;try{Engine.Run(o,cts.Token,s=>Console.WriteLine(s.Line()),Console.WriteLine);}finally{Console.CancelKeyPress-=cancel;}}return 0;}
 public static Options Parse(string[] args){var o=new Options();for(int i=0;i<args.Length;i++){string k=args[i];if(k=="--append"){o.Append=true;continue;}if(i+1>=args.Length)throw new ArgumentException("Missing option value: "+k);string v=args[++i];switch(k){case "--chain":o.Rule.Chain=v;break;case "--prefix":o.Rule.Prefix=v;break;case "--suffix":o.Rule.Suffix=v;break;case "--suffix-repeat":o.Rule.Repeat=int.Parse(v);break;case "--repeat-chars":o.Rule.CharChoice=v;break;case "--case":if(v!="sensitive"&&v!="insensitive")throw new ArgumentException("--case must be sensitive or insensitive");o.Rule.Sensitive=v=="sensitive";break;case "--count":o.Count=long.Parse(v);break;case "--seconds":o.Seconds=double.Parse(v,System.Globalization.CultureInfo.InvariantCulture);break;case "--output":o.Output=v;break;case "--output-dir":o.OutputDirectory=v;break;case "--format":o.Format=v;break;case "--groups":o.Groups=int.Parse(v);break;case "--batch":o.Batch=int.Parse(v);break;case "--reseed-seconds":o.ReseedSeconds=double.Parse(v,System.Globalization.CultureInfo.InvariantCulture);break;default:throw new ArgumentException("Unknown option: "+k);}}o.Validate();return o;}
 static string Ask(string text,string fallback){Console.Write(text+" ["+fallback+"]: ");string s=Console.ReadLine();return string.IsNullOrEmpty(s)?fallback:s;}
 static Options Menu(){Console.WriteLine("Ordinary account addresses. Omit T or 0x from the prefix. Output contains plaintext private keys.");var o=new Options();o.Rule.Chain=Ask("Network tron / eth / polygon","polygon");o.Rule.Prefix=Ask("Custom prefix","");if(Ask("Suffix mode fixed / repeat","repeat")=="repeat"){o.Rule.Repeat=int.Parse(Ask("Repeat length","6"));o.Rule.CharChoice=Ask("Set all/digits/letters/lower/upper or custom, e.g. 58aB","all");}else o.Rule.Suffix=Ask("Custom fixed suffix","");o.Rule.Sensitive=Ask("Case-sensitive y/n","n")=="y";o.Count=long.Parse(Ask("Count limit; 0 = unlimited","1"));o.Seconds=double.Parse(Ask("Time limit in seconds; 0 = unlimited","0"));o.OutputDirectory=Ask("Output folder; empty = current directory",Environment.CurrentDirectory);o.Format=Ask("Output format txt/xlsx/both","txt");o.Append=Ask("Append to network TXT file (txt only) y/n","n")=="y";o.Validate();return o;}
 static void Help(){Console.WriteLine("Astra v1.1.0 — Windows CUDA account address generator\nvanity.exe --gui                       Open the .NET graphical interface\nvanity.exe --self-test --extended       Extended crypto and GPU all-candidate checks\nvanity.exe --integration-test           Output, append, limits and seed-isolation checks\nvanity.exe --export-test                TXT/XLSX export checks\nvanity.exe --chain polygon --prefix ab --suffix 888 --count 10\nvanity.exe --chain tron --suffix-repeat 8 --repeat-chars digits --count 0\n\n--chain tron|eth|polygon   polygon = Polygon PoS mainnet, chain ID 137\n--prefix TEXT             Custom prefix, excluding T / 0x\n--suffix TEXT             Fixed suffix; mutually exclusive with repeat mode\n--suffix-repeat N         Last N characters identical; longer repeats also match\n--repeat-chars SET        all/digits/letters/lower/upper or a custom set, e.g. 58aB\n--case sensitive|insensitive   Default: insensitive; real checksum case is preserved\n--count N                 Default: 1; 0 = unlimited\n--seconds N               Default: 0 (unlimited); stop when either limit is reached\n--output-dir FOLDER       Existing folder; automatic filenames; default: current directory\n--format txt|xlsx|both     TXT / Excel / both; default: txt\n--output PATH             Explicit TXT filename (legacy option)\n--append                  Explicit TXT append; every run uses fresh random seeds\n--groups N                1–512; default: 256 independent random search groups\n--batch N                 1–64; default: 64; reduced automatically if too slow\n--reseed-seconds N        1–600; default: reseed unmatched groups every 600 seconds\n\nCtrl+C stops safely and flushes output. Files contain plaintext private keys. Store offline.\nM/s means million candidate addresses/s, not million matches/s. ETA is not guaranteed.\nEach random group delivers at most one wallet, then reseeds. Every hit is CPU-verified.");}
}
}
