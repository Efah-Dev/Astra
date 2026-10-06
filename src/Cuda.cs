using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
namespace Astra {
sealed class Cuda:IDisposable {
 const string Driver="nvcuda.dll",Rtc="nvrtc64_120_0.dll";
 [DllImport(Driver)] static extern int cuInit(uint flags);
 [DllImport(Driver)] static extern int cuDeviceGet(out int device,int ordinal);
 [DllImport(Driver)] static extern int cuDeviceGetName(StringBuilder name,int len,int dev);
 [DllImport(Driver)] static extern int cuDeviceGetAttribute(out int value,int attr,int dev);
 [DllImport(Driver)] static extern int cuDriverGetVersion(out int version);
 [DllImport(Driver)] static extern int cuDeviceTotalMem_v2(out UIntPtr bytes,int device);
 public static string[] Hardware(){Check(cuInit(0));int dev,major,minor,driver;UIntPtr bytes;Check(cuDeviceGet(out dev,0));var name=new StringBuilder(256);Check(cuDeviceGetName(name,256,dev));Check(cuDeviceTotalMem_v2(out bytes,dev));Check(cuDeviceGetAttribute(out major,75,dev));Check(cuDeviceGetAttribute(out minor,76,dev));Check(cuDriverGetVersion(out driver));string cpu="";try{using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))cpu=Convert.ToString(key.GetValue("ProcessorNameString")).Trim();}catch{cpu=Environment.ProcessorCount+" logical processors";}return new[]{name.ToString(),string.Format("VRAM {0:F1} GiB   ·   CUDA Driver API {1}.{2}   ·   Compute {3}.{4}",bytes.ToUInt64()/1073741824.0,driver/1000,(driver%1000)/10,major,minor),cpu};}
 [DllImport(Driver)] static extern int cuCtxCreate_v2(out IntPtr ctx,uint flags,int dev);
 [DllImport(Driver)] static extern int cuCtxDestroy_v2(IntPtr ctx);
 [DllImport(Driver)] static extern int cuModuleLoadData(out IntPtr module,byte[] data);
 [DllImport(Driver)] static extern int cuModuleUnload(IntPtr module);
 [DllImport(Driver)] static extern int cuModuleGetFunction(out IntPtr fn,IntPtr module,string name);
 [DllImport(Driver)] static extern int cuModuleGetGlobal_v2(out ulong ptr,out UIntPtr size,IntPtr module,string name);
 [DllImport(Driver)] static extern int cuMemAlloc_v2(out ulong ptr,UIntPtr size);
 [DllImport(Driver)] static extern int cuMemFree_v2(ulong ptr);
 [DllImport(Driver)] static extern int cuMemsetD8_v2(ulong ptr,byte value,UIntPtr size);
 [DllImport(Driver)] static extern int cuMemcpyHtoD_v2(ulong dst,byte[] src,UIntPtr size);
 [DllImport(Driver)] static extern int cuMemcpyDtoH_v2(byte[] dst,ulong src,UIntPtr size);
 [DllImport(Driver)] static extern int cuLaunchKernel(IntPtr f,uint gx,uint gy,uint gz,uint bx,uint by,uint bz,uint shared,IntPtr stream,IntPtr[] args,IntPtr extra);
 [DllImport(Driver)] static extern int cuCtxSynchronize();
 [DllImport(Driver)] static extern int cuGetErrorString(int code,out IntPtr s);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcCreateProgram(out IntPtr p,string src,string name,int headers,IntPtr hp,IntPtr names);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcCompileProgram(IntPtr p,int n,[In] string[] options);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcGetProgramLogSize(IntPtr p,out UIntPtr size);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcGetProgramLog(IntPtr p,byte[] log);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcGetPTXSize(IntPtr p,out UIntPtr size);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcGetPTX(IntPtr p,byte[] ptx);
 [DllImport(Rtc,CallingConvention=CallingConvention.Cdecl)] static extern int nvrtcDestroyProgram(ref IntPtr p);
 IntPtr context,module;Dictionary<ulong,int> allocations=new Dictionary<ulong,int>();Dictionary<string,IntPtr> functions=new Dictionary<string,IntPtr>();
 public string Device;public int Major,Minor,DriverVersion;public double CompileSeconds;
 public Cuda(){try{Check(cuInit(0));int dev;Check(cuDeviceGet(out dev,0));var name=new StringBuilder(256);Check(cuDeviceGetName(name,256,dev));Device=name.ToString();Check(cuDeviceGetAttribute(out Major,75,dev));Check(cuDeviceGetAttribute(out Minor,76,dev));Check(cuDriverGetVersion(out DriverVersion));Check(cuCtxCreate_v2(out context,4,dev));var sw=System.Diagnostics.Stopwatch.StartNew();string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"kernel-sm"+Major+Minor+".ptx");byte[] ptx=File.Exists(path)?File.ReadAllBytes(path):Compile(Major,Minor);Check(cuModuleLoadData(out module,ptx));CompileSeconds=sw.Elapsed.TotalSeconds;UploadTables();}catch{Dispose();throw;}}
 public static byte[] Compile(int major,int minor){IntPtr program;int rc=nvrtcCreateProgram(out program,File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"kernel.cu")),"kernel.cu",0,IntPtr.Zero,IntPtr.Zero);if(rc!=0)throw new Exception("NVRTC create "+rc);try{string[] options={"--gpu-architecture=compute_"+major+minor,"--std=c++11","--restrict","--use_fast_math","--device-as-default-execution-space"};rc=nvrtcCompileProgram(program,options.Length,options);UIntPtr size;nvrtcGetProgramLogSize(program,out size);byte[] log=new byte[(int)size.ToUInt64()];nvrtcGetProgramLog(program,log);if(rc!=0)throw new Exception("CUDA compilation failed: "+Encoding.UTF8.GetString(log));nvrtcGetPTXSize(program,out size);byte[] data=new byte[(int)size.ToUInt64()];if(nvrtcGetPTX(program,data)!=0)throw new Exception("NVRTC PTX failed");return data;}finally{nvrtcDestroyProgram(ref program);}}
 static void Check(int rc){if(rc!=0){IntPtr s;cuGetErrorString(rc,out s);throw new InvalidOperationException("CUDA error "+rc+": "+Marshal.PtrToStringAnsi(s));}}
 public ulong Alloc(int size){ulong p;Check(cuMemAlloc_v2(out p,(UIntPtr)size));allocations.Add(p,size);Clear(p);return p;}
 public void Free(ulong p){if(!allocations.ContainsKey(p))return;Clear(p);Check(cuMemFree_v2(p));allocations.Remove(p);}
 public void Clear(ulong p){Check(cuMemsetD8_v2(p,0,(UIntPtr)allocations[p]));}
 public void Put(ulong p,byte[] b){Check(cuMemcpyHtoD_v2(p,b,(UIntPtr)b.Length));}
 public byte[] Get(ulong p,int size){byte[] b=new byte[size];Check(cuMemcpyDtoH_v2(b,p,(UIntPtr)size));return b;}
 public void Constant(string name,byte[] b){ulong p;UIntPtr size;Check(cuModuleGetGlobal_v2(out p,out size,module,name));if(b.Length>(long)size.ToUInt64())throw new Exception("CUDA constant size: "+name);Put(p,b);}
 void UploadTables(){byte[] lanes=new byte[128*68],steps=new byte[65*68];var p=Crypto.Curve.G;for(int i=0;i<128;i++){Crypto.PackPoint(p).CopyTo(lanes,i*68);p=p.Add(Crypto.Curve.G);}p=Crypto.Curve.Curve.Infinity;var stride=Crypto.Curve.G.Multiply(Org.BouncyCastle.Math.BigInteger.ValueOf(128));for(int i=0;i<65;i++){Crypto.PackPoint(p).CopyTo(steps,i*68);p=p.Add(stride);}Constant("laneTable",lanes);Constant("stepTable",steps);}
 public void Launch(string name,int blocks,int threads,params object[] values){IntPtr fn;if(!functions.TryGetValue(name,out fn)){Check(cuModuleGetFunction(out fn,module,name));functions[name]=fn;}IntPtr[] args=new IntPtr[values.Length];try{for(int i=0;i<values.Length;i++){args[i]=Marshal.AllocHGlobal(8);if(values[i] is ulong)Marshal.WriteInt64(args[i],unchecked((long)(ulong)values[i]));else Marshal.WriteInt32(args[i],(int)values[i]);}Check(cuLaunchKernel(fn,(uint)blocks,1,1,(uint)threads,1,1,0,IntPtr.Zero,args,IntPtr.Zero));Check(cuCtxSynchronize());}finally{foreach(var p in args)if(p!=IntPtr.Zero)Marshal.FreeHGlobal(p);}}
 public void Dispose(){foreach(var a in allocations){cuMemsetD8_v2(a.Key,0,(UIntPtr)a.Value);cuMemFree_v2(a.Key);}allocations.Clear();if(module!=IntPtr.Zero){cuModuleUnload(module);module=IntPtr.Zero;}if(context!=IntPtr.Zero){cuCtxDestroy_v2(context);context=IntPtr.Zero;}}
}
}
