using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Security.AccessControl;
using System.Security.Principal;
namespace Astra {
sealed class Options {
 public Rules Rule=new Rules();public long Count=1;public double Seconds=0;public string Output="",OutputDirectory="",Format="txt";public bool Append=false;
 public int Groups=256,Batch=64;public double ReseedSeconds=600;
 public void Validate(){Rule.Validate();if(Count<0||Seconds<0||double.IsNaN(Seconds)||double.IsInfinity(Seconds))throw new ArgumentException("Count and seconds must be finite, non-negative values.");if(Groups<1||Groups>512||Batch<1||Batch>64)throw new ArgumentException("groups must be 1–512; batch must be 1–64.");if(ReseedSeconds<1||ReseedSeconds>600||double.IsNaN(ReseedSeconds))throw new ArgumentException("reseed-seconds must be 1–600.");Format=Format.ToLowerInvariant();if(Format!="txt"&&Format!="xlsx"&&Format!="both")throw new ArgumentException("Output format must be txt, xlsx or both.");if(Output.Length>0&&OutputDirectory.Length>0)throw new ArgumentException("--output and --output-dir cannot be combined.");if(OutputDirectory.Length>0&&!Directory.Exists(OutputDirectory))throw new ArgumentException("Output folder does not exist. Select an existing folder.");if(Output.Length>0&&(Format!="txt"||!string.Equals(Path.GetExtension(Output),".txt",StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Explicit file paths support TXT only. Use --output-dir for Excel.");if(Append&&Format!="txt")throw new ArgumentException("Append supports TXT only. Excel creates new files without changing existing results.");if(Append&&string.IsNullOrWhiteSpace(Output)&&string.IsNullOrWhiteSpace(OutputDirectory))throw new ArgumentException("Append requires an output folder or TXT file.");}
}
sealed class Snapshot {
 public string Network,Device,Output,Stage;public long Candidates,Accepted,Saved,Reseeds;public double Elapsed,Rate,Probability,InitSeconds,LastKernelMs;
 public long Target;public int GroupCandidates=8192;
 public string Line(){double effective=Probability*GroupCandidates<1e-6?Probability:(1-Math.Pow(1-Probability,GroupCandidates))/GroupCandidates;double mean=Rate>0?1/(Rate*effective):double.PositiveInfinity;return string.Format("{0} | {1:F3} M/s | candidates {2:N0} | flushed {3}/{4} | {5} | mean delivery≈{6} | remaining≈{7} | 95%≥1≈{8}",Network,Rate/1e6,Candidates,Saved,Target==0?"∞":Target.ToString(),Duration(Elapsed),Duration(mean),Target==0?"unlimited":Duration(mean*Math.Max(0,Target-Accepted)),Duration(mean*2.995732));}
 public static string Duration(double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds>3.15e15)return "very long/unknown";if(seconds<60)return seconds.ToString("F1")+"s";if(seconds<3600)return (seconds/60).ToString("F1")+"min";if(seconds<86400)return(seconds/3600).ToString("F1")+"h";return(seconds/86400).ToString("F1")+"d";}
}
sealed class Wallet {public string Address;public byte[] Key;}
sealed class SecureWriter:IDisposable {
 readonly BlockingCollection<Wallet> queue=new BlockingCollection<Wallet>(1024);readonly FileStream stream;readonly ExcelExport excel;readonly Task worker;readonly string label;Exception error;long saved;public string PathName;
 public long Saved{get{return Interlocked.Read(ref saved);}}
 public void Check(){var e=Volatile.Read(ref error);if(e!=null)throw new IOException("Private-key file write failed; search stopped. Flushed .pending data remains in the output folder.",e);}
 public SecureWriter(string path,bool append,string network,string chain):this(path,append,network,chain,"txt",""){}
 public SecureWriter(Options o):this(o.Output,o.Append,o.Rule.Label,o.Rule.Chain,o.Format,o.OutputDirectory){}
 SecureWriter(string path,bool append,string network,string chain,string format,string folder){
  label=network;folder=Path.GetFullPath(string.IsNullOrWhiteSpace(folder)?Environment.CurrentDirectory:folder);
  string stem=Path.Combine(folder,chain+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
  string txt=path.Length>0?Path.GetFullPath(path):append?Path.Combine(folder,chain+"_results.txt"):stem+".txt";
  try{
   if(format!="xlsx"){
    bool existing=append&&File.Exists(txt);if(append&&path.Length>0&&!existing)throw new IOException("Append file does not exist; omit --append for the first run.");
    stream=SecureFiles.Open(txt,existing);SecureFiles.Text(stream,(existing?"\r\n":"\uFEFF")+"# CUDA ETH / TRX | "+label+" | "+DateTimeOffset.Now.ToString("O")+" | PLAINTEXT PRIVATE KEYS: Do not share this file.\r\n");stream.Flush(true);
   }
   if(format!="txt")excel=new ExcelExport(stem,label);
   PathName=format=="txt"?txt:format=="xlsx"?stem+"_0001.xlsx":txt+" + Excel volumes";
   worker=Task.Factory.StartNew(WriteLoop,TaskCreationOptions.LongRunning);
  }catch{if(stream!=null)stream.Dispose();if(excel!=null)excel.Dispose();throw;}
 }
 void WriteLoop(){long pending=0;var flush=Stopwatch.StartNew();var publish=Stopwatch.StartNew();try{
  while(!queue.IsCompleted){Wallet w;if(queue.TryTake(out w,100)){try{
   if(stream!=null){SecureFiles.Text(stream,label+"\t"+w.Address+"\t");SecureFiles.Key(stream,w.Key);SecureFiles.Text(stream,"\r\n");}
   if(excel!=null)excel.Add(w);pending++;
  }finally{Crypto.Wipe(w.Key);}}
  if(flush.ElapsedMilliseconds>=1000){if(stream!=null)stream.Flush(true);if(excel!=null)excel.Flush();Interlocked.Add(ref saved,pending);pending=0;flush.Restart();}
  if(excel!=null&&publish.ElapsedMilliseconds>=5000){excel.Publish();publish.Restart();}
  }
  if(stream!=null)stream.Flush(true);if(excel!=null)excel.Finish();Interlocked.Add(ref saved,pending);
 }catch(Exception ex){Volatile.Write(ref error,ex);}finally{Wallet w;while(queue.TryTake(out w))Crypto.Wipe(w.Key);}}
 public void Add(Wallet w){try{while(!queue.TryAdd(w,100))Check();Check();}catch{Crypto.Wipe(w.Key);throw;}}
 public void Finish(){queue.CompleteAdding();worker.Wait();Check();}
 public void Dispose(){if(!queue.IsAddingCompleted)queue.CompleteAdding();worker.Wait();if(stream!=null)stream.Dispose();if(excel!=null)excel.Dispose();queue.Dispose();}
}
static class Engine {
 public const int HitSize=120;
 public static string HitAddress(byte[] b,int index){int start=index*HitSize+72,len=0;while(len<43&&b[start+len]!=0)len++;return Encoding.ASCII.GetString(b,start,len);}
 public static bool Found(byte[] b,int i){return BitConverter.ToInt32(b,i*HitSize+116)!=0;}
 public static Snapshot Run(Options o,CancellationToken token,Action<Snapshot> status,Action<string> message){
  o.Validate();var init=Stopwatch.StartNew();message("Running CPU known-vector and CUDA all-candidate startup checks…");Crypto.KnownTests();
  using(var gpu=new Cuda()){
   SelfTest.Gpu(gpu,false);message("Device: "+gpu.Device+" | CUDA driver API "+gpu.DriverVersion+" | Initialization/load "+gpu.CompileSeconds.ToString("F2")+" seconds");
   gpu.Constant("rule",o.Rule.Pack());
   using(var writer=new SecureWriter(o)){
    message(o.Rule.Description);if(o.Format!="txt")message("Background Excel export: publishes an XLSX every 5 seconds or 10,000 rows; intermediate data is flushed each second. Stop completes the remaining export.");message("Output: "+writer.PathName+"(plaintext private keys; access limited to the current user and SYSTEM)");message("M/s = million candidate addresses per second. ETA is probabilistic; TRON checksums and EIP-55 use a random-hash model.");
    ulong states=gpu.Alloc(o.Groups*128*68),bases=gpu.Alloc(o.Groups*68),reset=gpu.Alloc(o.Groups*4),hits=gpu.Alloc(o.Groups*HitSize);
    byte[][] seeds=new byte[o.Groups][];ulong[] starts=new ulong[o.Groups];double[] ages=new double[o.Groups];bool[] replace=Enumerable.Repeat(true,o.Groups).ToArray();ulong offset=0;
    Snapshot snap=new Snapshot{Network=o.Rule.Chain,Device=gpu.Device,Output=writer.PathName,Probability=o.Rule.Probability,Target=o.Count,InitSeconds=init.Elapsed.TotalSeconds,Stage="Searching"};
    Stopwatch clock=Stopwatch.StartNew(),refresh=Stopwatch.StartNew();long lastCandidates=0;double lastTime=0;int batch=o.Batch;
    try{while(!token.IsCancellationRequested&&(o.Count==0||snap.Accepted<o.Count)&&(o.Seconds==0||clock.Elapsed.TotalSeconds<o.Seconds)){
     writer.Check();bool need=false;for(int g=0;g<o.Groups;g++){if(clock.Elapsed.TotalSeconds-ages[g]>=o.ReseedSeconds||offset-starts[g]>=(1UL<<40))replace[g]=true;need|=replace[g];}
     if(need){byte[] baseData=new byte[o.Groups*68],flags=new byte[o.Groups*4];try{for(int g=0;g<o.Groups;g++)if(replace[g]){Crypto.Wipe(seeds[g]);seeds[g]=Crypto.RandomScalar();Crypto.PackPoint(Crypto.Public(seeds[g])).CopyTo(baseData,g*68);flags[g*4]=1;starts[g]=offset;ages[g]=clock.Elapsed.TotalSeconds;snap.Reseeds++;replace[g]=false;}gpu.Put(bases,baseData);gpu.Put(reset,flags);gpu.Launch("initialize",o.Groups,128,states,bases,reset);}finally{Crypto.Wipe(baseData);Crypto.Wipe(flags);gpu.Clear(bases);gpu.Clear(reset);}}
     if(token.IsCancellationRequested||(o.Seconds>0&&clock.Elapsed.TotalSeconds>=o.Seconds))break;
     gpu.Clear(hits);var kernel=Stopwatch.StartNew();gpu.Launch("search",o.Groups,128,states,hits,offset,batch);snap.LastKernelMs=kernel.Elapsed.TotalMilliseconds;
     byte[] data=gpu.Get(hits,o.Groups*HitSize);snap.Candidates+=(long)o.Groups*128*batch;offset+=(ulong)batch*128;
     try{for(int g=0;g<o.Groups;g++)if(Found(data,g)){
      replace[g]=true;ulong at=BitConverter.ToUInt64(data,g*HitSize);if(at<=starts[g]||at>offset)throw new Exception("GPU private-key offset verification failed.");
      byte[] key=Crypto.ScalarAt(seeds[g],at-starts[g]);try{var pub=Crypto.Public(key);var expected=Crypto.PubBytes(pub);for(int j=0;j<64;j++)if(expected[j]!=data[g*HitSize+8+j])throw new Exception("GPU public key differs from independent CPU verification; stopped.");string address=Crypto.Address(pub,o.Rule.Tron);if(address!=HitAddress(data,g)||!o.Rule.Match(address))throw new Exception("GPU address or match differs from CPU verification; stopped.");
       if(o.Count==0||snap.Accepted<o.Count){writer.Add(new Wallet{Address=address,Key=key});key=null;snap.Accepted++;}
      }finally{Crypto.Wipe(key);Crypto.Wipe(seeds[g]);seeds[g]=null;}
     }}finally{Crypto.Wipe(data);}
     // Bound launches for desktop responsiveness. Never increase beyond the explicit user cap.
     if(snap.LastKernelMs>200&&batch>1)batch=Math.Max(1,batch/2);snap.GroupCandidates=batch*128;
     if(refresh.ElapsedMilliseconds>=1000){snap.Elapsed=clock.Elapsed.TotalSeconds;snap.Rate=(snap.Candidates-lastCandidates)/(snap.Elapsed-lastTime);lastCandidates=snap.Candidates;lastTime=snap.Elapsed;snap.Saved=writer.Saved;status(snap);refresh.Restart();}
    }
    writer.Finish();snap.Elapsed=clock.Elapsed.TotalSeconds;snap.Saved=writer.Saved;snap.Rate=snap.Elapsed>0?snap.Candidates/snap.Elapsed:0;snap.Stage="Stopped safely";status(snap);message("Stopped safely; verified and flushed "+snap.Saved+" records. Independent reseeds: "+snap.Reseeds+".");message(string.Format(System.Globalization.CultureInfo.InvariantCulture,"METRICS candidates={0} saved={1} seconds={2:F6} candidates_per_second={3:F3} saved_per_second={4:F3} initialization_seconds={5:F6} last_kernel_ms={6:F3} reseeds={7}",snap.Candidates,snap.Saved,snap.Elapsed,snap.Rate,snap.Saved/Math.Max(snap.Elapsed,0.000001),snap.InitSeconds,snap.LastKernelMs,snap.Reseeds));return snap;
    }finally{foreach(byte[] s in seeds)Crypto.Wipe(s);gpu.Free(states);gpu.Free(bases);gpu.Free(reset);gpu.Free(hits);}
   }
  }
 }
}
}

