using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
class ConsoleInterrupt {
 [DllImport("kernel32.dll")] static extern bool FreeConsole();
 [DllImport("kernel32.dll")] static extern bool AllocConsole();
 [DllImport("kernel32.dll")] static extern bool AttachConsole(uint process);
 [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(IntPtr handler,bool add);
 [DllImport("kernel32.dll")] static extern bool GenerateConsoleCtrlEvent(uint type,uint group);
 static int Main(string[] args){
  string report=Path.GetFullPath(args[1]);try{
   FreeConsole();if(!AllocConsole())throw new Exception("AllocConsole failed");
   string outputArgs=args.Length>3?" --format both --output-dir ":" --output ";
   var psi=new ProcessStartInfo(Path.GetFullPath(args[0]),"--chain polygon --suffix-repeat 2 --count 0 --groups 16 --batch 4"+outputArgs+"\""+Path.GetFullPath(args[2])+"\""){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8,CreateNoWindow=false,WorkingDirectory=Path.GetDirectoryName(report)};
   using(var p=Process.Start(psi)){var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();Thread.Sleep(5000);FreeConsole();if(!AttachConsole((uint)p.Id))throw new Exception("Attach child console failed "+Marshal.GetLastWin32Error());SetConsoleCtrlHandler(IntPtr.Zero,true);var sw=Stopwatch.StartNew();if(!GenerateConsoleCtrlEvent(0,0))throw new Exception("Generate CTRL_C_EVENT failed");if(!p.WaitForExit(15000)){p.Kill();p.WaitForExit();File.WriteAllText(report,stdout.Result+stderr.Result);throw new Exception("Ctrl+C timeout");}File.WriteAllText(report,stdout.Result+stderr.Result+"\r\nCTRL_C_EVENT exit="+p.ExitCode+" stop_ms="+sw.Elapsed.TotalMilliseconds.ToString("F1"));return p.ExitCode;}
  }catch(Exception ex){File.WriteAllText(report,ex.ToString());return 1;}
 }
}
