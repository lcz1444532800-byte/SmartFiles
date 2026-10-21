using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace SmartFiles.Core;
public static class FileIdentity {
 [StructLayout(LayoutKind.Sequential)] struct Info {public uint Attr;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFileW(string name,uint access,uint share,IntPtr sec,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle h,out Info info);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetFileInformationByHandle(SafeFileHandle h,int cls,IntPtr buffer,uint size);
 public static SafeFileHandle Open(string path,bool mutate=false,bool directory=false) {
  var h=CreateFileW(path,mutate?0x80010000u:0x80000000u,mutate?1u:7u,IntPtr.Zero,3,directory?0x02200000u:0x00200000u,IntPtr.Zero);
  if(h.IsInvalid){h.Dispose();throw new Win32Exception(Marshal.GetLastWin32Error());} return h;
 }
 public static Stamp Read(SafeFileHandle h) {
  if(!GetFileInformationByHandle(h,out var x))throw new Win32Exception(Marshal.GetLastWin32Error());
  if((x.Attr&0x400)!=0||x.Links!=1)throw new IOException("不处理重解析点或硬链接");
  return new($"{x.Volume:X8}:{x.IndexHigh:X8}{x.IndexLow:X8}",((long)x.SizeHigh<<32)|x.SizeLow,((long)x.Write.dwHighDateTime<<32)|(uint)x.Write.dwLowDateTime);
 }
 public static Stamp Read(string p){using var h=Open(p);return Read(h);}
 public static bool Matches(string p,Stamp s){try{return Read(p)==s;}catch{return false;}}
 public static void Rename(SafeFileHandle h,string target) {
  var bytes=System.Text.Encoding.Unicode.GetBytes(target);
  int offset=IntPtr.Size==8?20:12; int rootOffset=IntPtr.Size==8?8:4;
  int total=offset+bytes.Length+2;var mem=Marshal.AllocHGlobal(total);
  try {for(int i=0;i<total;i++)Marshal.WriteByte(mem,i,0);Marshal.WriteIntPtr(mem,rootOffset,IntPtr.Zero);Marshal.WriteInt32(mem,offset-4,bytes.Length);Marshal.Copy(bytes,0,mem+offset,bytes.Length);
   if(!SetFileInformationByHandle(h,3,mem,(uint)total))throw new Win32Exception(Marshal.GetLastWin32Error());
  }finally{Marshal.FreeHGlobal(mem);}
 }
 public static void DeleteOpened(SafeFileHandle h) {var m=Marshal.AllocHGlobal(1);try{Marshal.WriteByte(m,1);if(!SetFileInformationByHandle(h,4,m,1))throw new Win32Exception(Marshal.GetLastWin32Error());}finally{Marshal.FreeHGlobal(m);}}
 public static void ValidatePath(string path) {
  var full=Path.GetFullPath(path); if(full.StartsWith(@"\\")||full.Length<3||full[1]!=':'||full.AsSpan(2).Contains(':'))throw new IOException("不允许网络路径或数据流");
  for(string? p=full;p!=null;p=Path.GetDirectoryName(p)){
   if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("不处理符号链接或 junction");
  }
 }
 public static string SafeName(string name) {
  if(string.IsNullOrWhiteSpace(name)||name.Length>150||name!=name.TrimEnd(' ','.')||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name is "." or "..")throw new IOException("名称无效");
  var stem=name.Split('.')[0].ToUpperInvariant();
  if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem))throw new IOException("保留名称");
  return name;
 }
 public static bool Under(string path,string root)=>Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);
 public static IDisposable PinParents(params string[] paths)=>new DirectoryPins(paths);
 sealed class DirectoryPins : IDisposable {
  readonly List<SafeFileHandle> pins=[];
  public DirectoryPins(string[] paths){try{foreach(var p in paths.SelectMany(Ancestors).Distinct(StringComparer.OrdinalIgnoreCase)){ValidatePath(p);var h=CreateFileW(p,0x80,3,IntPtr.Zero,3,0x02200000,IntPtr.Zero);if(h.IsInvalid){h.Dispose();throw new Win32Exception(Marshal.GetLastWin32Error());}pins.Add(h);ValidatePath(p);}}catch{Dispose();throw;}}
  static IEnumerable<string> Ancestors(string p){for(var d=Path.GetDirectoryName(Path.GetFullPath(p));d!=null;d=Path.GetDirectoryName(d))yield return d;}
  public void Dispose(){foreach(var h in pins)h.Dispose();}
 }
}
