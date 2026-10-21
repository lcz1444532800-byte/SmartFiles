using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace SmartFiles.Core;
internal sealed class ChildJob:IDisposable {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern SafeFileHandle CreateJobObjectW(IntPtr attributes,string? name);
 [DllImport("kernel32.dll")]static extern bool SetInformationJobObject(SafeFileHandle job,int cls,IntPtr info,uint length);
 [DllImport("kernel32.dll")]static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
 readonly SafeFileHandle job=CreateJobObjectW(IntPtr.Zero,null);
 public ChildJob(){int size=IntPtr.Size==8?144:112;var data=Marshal.AllocHGlobal(size);try{for(int i=0;i<size;i++)Marshal.WriteByte(data,i,0);Marshal.WriteInt32(data,16,0x2000);if(!SetInformationJobObject(job,9,data,(uint)size))throw new System.ComponentModel.Win32Exception();}finally{Marshal.FreeHGlobal(data);}}
 public void Add(System.Diagnostics.Process process){if(!AssignProcessToJobObject(job,process.Handle))throw new System.ComponentModel.Win32Exception();}
 public void Dispose()=>job.Dispose();
}
