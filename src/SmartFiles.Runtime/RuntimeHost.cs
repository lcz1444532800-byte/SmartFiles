using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
namespace SmartFiles.Core;
public sealed class WorkItem {public FileTask Task {get;set;}=new();public int Attempts {get;set;}}
[JsonSerializable(typeof(Settings))][JsonSerializable(typeof(WorkItem))][JsonSerializable(typeof(FileTask))]
public partial class RuntimeJson:JsonSerializerContext{}
public static class RuntimeHost {
 public const string DefaultHome=@"D:\SmartFilesApp";
 public static string Home(string[] args){var i=Array.IndexOf(args,"--home");var home=i>=0&&i+1<args.Length?Path.GetFullPath(args[i+1]):DefaultHome;if(!home.StartsWith(@"D:\",StringComparison.OrdinalIgnoreCase))throw new IOException("Runtime data must be stored on drive D:");return home;}
 public static string Name(string home,string role)=>@"Local\SmartFiles."+role+"."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(home).ToUpperInvariant())))[..16];
 public static string Queue(string home){var path=Path.Combine(home,"Data","Queue");FileIdentity.ValidatePath(path);Directory.CreateDirectory(path);return path;}
 public static Settings ReadSettings(string home)=>JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(home,"Data","settings.json")),RuntimeJson.Default.Settings)??new();
 public static WorkItem ReadJob(string path)=>JsonSerializer.Deserialize(File.ReadAllText(path),RuntimeJson.Default.WorkItem)??throw new IOException("Invalid task");
 public static void WriteJob(string path,WorkItem value){File.WriteAllText(path+".new",JsonSerializer.Serialize(value,RuntimeJson.Default.WorkItem));File.Move(path+".new",path,true);}
 public static void Signal(string home,string role){try{using var signal=EventWaitHandle.OpenExisting(Name(home,role));signal.Set();}catch(WaitHandleCannotBeOpenedException){}}
 public static bool IsRunning(string home,string role){try{using var signal=EventWaitHandle.OpenExisting(Name(home,role));return true;}catch(WaitHandleCannotBeOpenedException){return false;}}
 public static Process Launch(string exe,string home,params string[] args){var start=new ProcessStartInfo(Path.Combine(DefaultHome,"Release",exe=="SmartFiles.Watcher.exe"?"Watcher":exe=="SmartFiles.Worker.exe"?"Worker":"",exe)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=DefaultHome};start.ArgumentList.Add("--home");start.ArgumentList.Add(home);foreach(var arg in args)start.ArgumentList.Add(arg);start.Environment["TEMP"]=Path.Combine(home,"Temp");start.Environment["TMP"]=Path.Combine(home,"Temp");Directory.CreateDirectory(start.Environment["TEMP"]!);return Process.Start(start)??throw new IOException("Process could not start");}
 public static void EnsureWatcher(string home){if(!IsRunning(home,"Watcher.Reload"))using(Launch("SmartFiles.Watcher.exe",home)){}Signal(home,"Watcher.Reload");}
 public static void StopAll(string home){Signal(home,"Watcher.Exit");Signal(home,"Worker.Exit");}
 public static void Manual(string path,string note){var job=ReadJob(path);job.Task.Status="Pending";job.Task.Method="Manual review needed";job.Task.Note=note;WriteJob(Path.ChangeExtension(path,"manual"),job);File.Delete(path);}
}
