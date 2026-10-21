using System.Diagnostics;
using SmartFiles.Core;
namespace SmartFiles.Watcher;
static class Program {
 static readonly object gate=new();static readonly List<SourceWatcher> watchers=[];static Process? worker;static string home="",queue="",signature="",batch=Guid.NewGuid().ToString("N");static Settings settings=new();static DateTime lastSeen;static bool exiting;static int failures;static Timer? retry;
 static void Main(string[] args){
  home=RuntimeHost.Home(args);queue=RuntimeHost.Queue(home);using var singleton=new Mutex(true,RuntimeHost.Name(home,"Watcher.Singleton"),out var first);if(!first){RuntimeHost.Signal(home,"Watcher.Reload");return;}
  using var quit=new EventWaitHandle(false,EventResetMode.ManualReset,RuntimeHost.Name(home,"Watcher.Exit"));using var reload=new EventWaitHandle(false,EventResetMode.AutoReset,RuntimeHost.Name(home,"Watcher.Reload"));
  using var settingsWatch=new FileSystemWatcher(Path.Combine(home,"Data"),"settings.json"){NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite};settingsWatch.Changed+=(_,_)=>reload.Set();settingsWatch.Created+=(_,_)=>reload.Set();settingsWatch.Renamed+=(_,_)=>reload.Set();settingsWatch.EnableRaisingEvents=true;
  if(args.Contains("--startup")&&quit.WaitOne(TimeSpan.FromSeconds(20)))return;
  try{Reload();Pump();while(WaitHandle.WaitAny([quit,reload])==1){try{Reload();Pump();}catch{Fault("reload_failed");}}}
  catch(Exception ex){Fault("watcher_start_"+ex.GetType().Name);}
  finally{lock(gate){exiting=true;retry?.Dispose();foreach(var w in watchers)w.Dispose();watchers.Clear();RuntimeHost.Signal(home,"Worker.Exit");}if(worker is {HasExited:false}&&!worker.WaitForExit(8000))try{worker.Kill(true);}catch{}worker?.Dispose();}
 }
 static void Reload(){var latest=RuntimeHost.ReadSettings(home);var mark=latest.Monitoring+"|"+latest.BackgroundMode+"|"+string.Join(";",latest.Sources.Select(s=>$"{s.Enabled}|{s.Path}|{s.Recursive}|{s.Policy}"));settings=latest;if(mark==signature)return;lock(gate){foreach(var w in watchers)w.Dispose();watchers.Clear();signature=mark;if(!latest.Monitoring||latest.BackgroundMode!="Realtime")return;
  foreach(var s in latest.Sources.Where(x=>x.Enabled&&x.Policy!=SourcePolicy.Ignore))try{var path=Path.GetFullPath(s.Path);if(path==Path.GetPathRoot(path)||FileIdentity.Under(path,home)||path.Equals(home,StringComparison.OrdinalIgnoreCase)||((path.Equals(latest.Root,StringComparison.OrdinalIgnoreCase)||FileIdentity.Under(path,latest.Root))&&!path.Equals(Path.Combine(latest.Root,"_Inbox"),StringComparison.OrdinalIgnoreCase)))throw new IOException("Invalid source scope");watchers.Add(new(s,Accept,Vanished,_=>Fault("source_paused")));}catch{Fault("source_unavailable");}
 }File.WriteAllText(Path.Combine(home,"Data","watcher-status.json"),$"{{\"sources\":{watchers.Count},\"readyUtc\":\"{DateTime.UtcNow:O}\"}}");}
 static void Accept(string path,Source source,Stamp stamp,DateTime seen){try{lock(gate){if(exiting||!settings.Monitoring||!settings.Classification)return;if(Directory.EnumerateFiles(queue).Take(10001).Count()>10000){Fault("queue_capacity_reached");return;}if((seen-lastSeen).TotalMilliseconds>settings.BatchWindowMs)batch=Guid.NewGuid().ToString("N");lastSeen=seen;var task=new FileTask{Path=path,SourcePath=source.Path,SourceName=source.Name,Stamp=stamp,SeenUtc=seen,BatchId=batch,Status="Analyzing"};RuntimeHost.WriteJob(Path.Combine(queue,task.Id+".job"),new(){Task=task});failures=0;Pump();}}catch(Exception e){Fault("queue_write_"+e.GetType().Name);}}
 static void Vanished(string path){RuntimeHost.Signal(home,"Worker.Wake");}
 static void Pump(){lock(gate){if(exiting)return;if(worker is {HasExited:false}){RuntimeHost.Signal(home,"Worker.Wake");return;}if(!Directory.EnumerateFiles(queue,"*.job").Any())return;worker?.Dispose();try{worker=RuntimeHost.Launch("SmartFiles.Worker.exe",home);worker.EnableRaisingEvents=true;worker.Exited+=WorkerExited;if(worker.HasExited)WorkerExited(worker,EventArgs.Empty);}catch{Retry();}}}
 static int exitedPid;
 static void WorkerExited(object? sender,EventArgs e){lock(gate){if(exiting||sender is not Process p||p.Id==exitedPid)return;exitedPid=p.Id;if(p.ExitCode!=0){Retry();return;}failures=0;retry?.Dispose();retry=new(_=>Pump(),null,300,Timeout.Infinite);}}
 static void Retry(){failures++;if(failures>=3){foreach(var path in Directory.EnumerateFiles(queue,"*.job"))try{RuntimeHost.Manual(path,"Worker failed three times. Automatic retries stopped; select a category manually.");}catch{}Fault("worker_retry_limit");return;}retry?.Dispose();retry=new(_=>Pump(),null,failures*1000,Timeout.Infinite);}
 static void Fault(string code){try{var path=Path.Combine(home,"Logs");Directory.CreateDirectory(path);var file=Path.Combine(path,"watcher.log");if(File.Exists(file)&&new FileInfo(file).Length>1024*1024)File.Move(file,file+".1",true);File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" "+code+Environment.NewLine);}catch{}}
}
