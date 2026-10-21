using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
namespace SmartFiles.Core;
public sealed class SettingsService {
 public string Home {get;} public Settings Value {get;private set;}
 public SettingsService(string home) {
  Home=System.IO.Path.GetFullPath(home);FileIdentity.ValidatePath(Home);
  if (!Home.StartsWith(@"D:\",StringComparison.OrdinalIgnoreCase)||!Directory.Exists(@"D:\")) throw new IOException("D 盘不可用；禁止回退 C 盘");
  Directory.CreateDirectory(System.IO.Path.Combine(Home,"Data"));
  var p=System.IO.Path.Combine(Home,"Data","settings.json"); Value=File.Exists(p)?Json.Decode<Settings>(File.ReadAllText(p)):new();
 }
 public void Reload(){Value=Json.Decode<Settings>(File.ReadAllText(System.IO.Path.Combine(Home,"Data","settings.json")));}
 public static void Validate(Settings Value) {
  NormalizeEndpoint(Value.BaseUrl);
  if(!double.IsFinite(Value.LocalThreshold)||!double.IsFinite(Value.HighThreshold))throw new InvalidDataException("阈值必须是有效数字");
  if(Value.WorkerIdleSeconds is <30 or >600||Value.HistoryMaxCount is <100 or >100000||Value.HistoryMaxDays is <1 or >3650)throw new InvalidDataException("后台或历史保留参数超出范围");
  if(Value.BatchWindowMs is <500 or >5000||Value.ImageApiBudgetPerBatch is <0 or >30)throw new InvalidDataException("批次时间应为 500–5000 毫秒，图片上限应为 0–30");
  if(Value.CandidateCount is <5 or >15||Value.ApiConcurrency is <1 or >2||Value.LocalThreshold is <.5 or >1||Value.HighThreshold<Value.LocalThreshold||Value.HighThreshold>1||Value.InteractiveTtlMs<0||Value.ApiTimeoutSeconds is <1 or >120) throw new InvalidDataException("设置超出安全范围");
  if(!Value.Root.StartsWith(@"D:\",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("分类根目录必须在 D 盘");
 }
 readonly object saveGate=new();
 public void Save()=>Replace(Value);
 public void Replace(Settings candidate){Validate(candidate);lock(saveGate){
  var p=System.IO.Path.Combine(Home,"Data","settings.json");File.WriteAllText(p+".new",Json.Encode(candidate));File.Move(p+".new",p,true);Value=candidate;}
 }
 public static string NormalizeEndpoint(string url){
  if(!Uri.TryCreate(url.Trim().TrimEnd('/'),UriKind.Absolute,out var uri)||(!uri.IsLoopback&&uri.Scheme!="https")||(uri.Scheme!="https"&&uri.Scheme!="http")||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)throw new InvalidDataException("请填写有效的 HTTPS API 基础地址");
  return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
 }
 string KeyPath(string endpoint)=>System.IO.Path.Combine(Home,"Data","api-key-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeEndpoint(endpoint))))+".dpapi");
 public void SaveKey(string key,string? endpoint=null) {
  var p=KeyPath(endpoint??Value.BaseUrl);
  File.WriteAllBytes(p+".new",ProtectedData.Protect(Encoding.UTF8.GetBytes(key),null,DataProtectionScope.CurrentUser));File.Move(p+".new",p,true);
 }
 public string ReadKey(string? endpoint=null) {
  var url=NormalizeEndpoint(endpoint??Value.BaseUrl);var p=KeyPath(url);
  if(!File.Exists(p)&&url=="https://api.deepseek.com")p=System.IO.Path.Combine(Home,"Data","api-key.dpapi");
  return File.Exists(p)?Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(p),null,DataProtectionScope.CurrentUser)):"";
 }
 public void SetStartup(bool enabled,string exe) {
  using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
  if(enabled)key.SetValue("SmartFiles",$"\"{System.IO.Path.Combine(RuntimeHost.DefaultHome,"Release","Watcher","SmartFiles.Watcher.exe")}\" --startup"); else key.DeleteValue("SmartFiles",false);
  Value.AutoStart=enabled;Save();
 }
}
public sealed class SafeLog {
 readonly string folder; readonly object gate=new();
 public SafeLog(string home){folder=Path.Combine(home,"Logs");Directory.CreateDirectory(folder);}
 public void Write(string code,Exception? e=null) {lock(gate){
  var p=Path.Combine(folder,"smartfiles.log");
  if(File.Exists(p)&&new FileInfo(p).Length>5*1024*1024){
   for(int i=3;i>=1;i--){var a=p+"."+i; if(File.Exists(a))File.Move(a,p+"."+(i+1),true);}
   File.Move(p,p+".1",true);
  }
  File.AppendAllText(p,$"{DateTime.UtcNow:O} {code} {e?.GetType().Name} {e?.HResult}\n");
 }}
}
