using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SmartFiles.Core;
public interface ISourceAdapter { IEnumerable<Source> Discover(); }
public sealed class SourceDiscovery {
 readonly ISourceAdapter[] adapters=[new BrowserSourceAdapter(),new ChatSourceAdapter()];
 public List<Source> Discover(string root) {
  var all=adapters.SelectMany(x=>x.Discover()).Where(x=>Directory.Exists(x.Path)).ToList();
  all.Add(new(){Name="主动投递 · _Inbox",Path=Path.Combine(root,"_Inbox"),Enabled=true});
  return all.GroupBy(x=>Path.GetFullPath(x.Path),StringComparer.OrdinalIgnoreCase).Select(g=>new Source{Name=string.Join(" / ",g.Select(x=>x.Name).Distinct()),Path=g.Key,Enabled=g.Any(x=>x.Enabled),Recursive=g.Any(x=>x.Recursive),Policy=g.Any(x=>x.Policy==SourcePolicy.AskBeforeMove)?SourcePolicy.AskBeforeMove:SourcePolicy.NormalMove}).ToList();
 }
}
public sealed class BrowserSourceAdapter : ISourceAdapter {
 [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
 public IEnumerable<Source> Discover() {
  var result=new List<Source>();var id=new Guid("374DE290-123F-4565-9164-39C4925E467B");IntPtr ptr=IntPtr.Zero;
  try{if(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out ptr)==0)result.Add(new(){Name="Windows Downloads",Path=Marshal.PtrToStringUni(ptr)!});}finally{if(ptr!=IntPtr.Zero)Marshal.FreeCoTaskMem(ptr);}
  var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  foreach(var (name,rel) in new[]{("Chrome",@"Google\Chrome\User Data"),("Edge",@"Microsoft\Edge\User Data")}){
   var folder=Path.Combine(local,rel);if(!Directory.Exists(folder))continue;
   foreach(var profile in Directory.EnumerateDirectories(folder).Where(p=>Path.GetFileName(p)=="Default"||Path.GetFileName(p).StartsWith("Profile "))){
    try{var p=Path.Combine(profile,"Preferences");if(!File.Exists(p)||new FileInfo(p).Length>20_000_000)continue;using var doc=JsonDocument.Parse(File.ReadAllText(p));if(doc.RootElement.TryGetProperty("download",out var dl)&&dl.TryGetProperty("default_directory",out var dir)&&!string.IsNullOrWhiteSpace(dir.GetString()))result.Add(new(){Name=name,Path=dir.GetString()!});}catch{}
   }
  }
  var firefox=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Mozilla\Firefox\Profiles");
  if(Directory.Exists(firefox))foreach(var d in Directory.EnumerateDirectories(firefox))try{
   var p=Path.Combine(d,"prefs.js");if(File.Exists(p))foreach(var line in File.ReadLines(p)){var m=Regex.Match(line,@"user_pref\(""browser.download.dir"",\s*("".*?"")\)");if(m.Success)result.Add(new(){Name="Firefox",Path=System.Text.Json.JsonSerializer.Deserialize<string>(m.Groups[1].Value)!});}
  }catch{}
  return result;
 }
}
public sealed class ChatSourceAdapter : ISourceAdapter {
 public IEnumerable<Source> Discover() {
  var results=new List<Source>();
  // Only inspect documented/configured roots, then recognized receive leaves. Never watch a chat root.
  var roots=new List<(string,string)>{("微信",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"WeChat Files")),("QQ",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Tencent Files"))};
  using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Tencent\WeChat")){
   if(k?.GetValue("FileSavePath") is string p&&Path.IsPathRooted(p)){roots.Add(("微信",p));roots.Add(("微信",Path.Combine(p,"WeChat Files")));}
  }
  roots.Add(("微信",@"D:\微信记录\xwechat_files"));roots.Add(("微信",@"D:\微信记录\WeChat Files"));roots.Add(("QQ",@"D:\qq记录\Tencent Files"));
  if(Directory.Exists(@"D:\QQ下载"))results.Add(new(){Name="QQ保存目录（请核对）",Path=@"D:\QQ下载",Recursive=true,Policy=SourcePolicy.AskBeforeMove});
  foreach(var (name,root) in roots.Distinct()){
   if(!Directory.Exists(root))continue;
   foreach(var account in Directory.EnumerateDirectories(root).Take(50)){
    foreach(var relative in name=="微信"?new[]{@"FileStorage\File",@"msg\file"}:new[]{"FileRecv"}){
     var p=Path.Combine(account,relative);
     if(!Directory.Exists(p))continue;
     results.Add(new(){Name=name+" 接收文件",Path=p,Recursive=true,Policy=SourcePolicy.AskBeforeMove});
    }
   }
  }return results;
 }
}
