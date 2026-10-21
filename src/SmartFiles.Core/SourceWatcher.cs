using System.Collections.Concurrent;
using System.Threading.Channels;
namespace SmartFiles.Core;
public sealed class SourceWatcher : IDisposable {
 readonly Source source;readonly FileSystemWatcher watcher;
 readonly ConcurrentDictionary<string,byte> baseline=new();readonly ConcurrentDictionary<string,(string Path,DateTime Seen)> tracked=new(StringComparer.OrdinalIgnoreCase);
 readonly Channel<(string,string,string)> events=Channel.CreateBounded<(string,string,string)>(new BoundedChannelOptions(4096){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
 readonly CancellationTokenSource stop=new();readonly Action<string,Source,Stamp,DateTime> ready;readonly Action<string> vanished;readonly Action<string> error;
 readonly Task loop;volatile bool armed;
 public static bool Temporary(string p)=>new[]{".crdownload",".part",".tmp",".download",".partial",".aria2",".lock"}.Contains(Path.GetExtension(p).ToLowerInvariant());
 public SourceWatcher(Source src,Action<string,Source,Stamp,DateTime> callback,Action<string> gone,Action<string> fail) {
  source=src;ready=callback;vanished=gone;error=fail;FileIdentity.ValidatePath(src.Path);
  watcher=new(src.Path){IncludeSubdirectories=src.Recursive,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite|NotifyFilters.Size,InternalBufferSize=32*1024};
  watcher.Created+=(_,e)=>Queue("created",e.FullPath,"");watcher.Changed+=(_,e)=>Queue("changed",e.FullPath,"");
  watcher.Renamed+=(_,e)=>Queue("renamed",e.FullPath,e.OldFullPath);watcher.Deleted+=(_,e)=>Queue("deleted",e.FullPath,"");
  watcher.Error+=(_,_)=>Overflow();
  try{watcher.EnableRaisingEvents=true;
  // Metadata-only baseline. Events during arming are deliberately excluded, never promoted as new.
  foreach(var f in Directory.EnumerateFiles(src.Path,"*",new EnumerationOptions{RecurseSubdirectories=src.Recursive,AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=false}))try{baseline.TryAdd(FileIdentity.Read(f).Id,0);}catch{}
  armed=true;loop=Task.Run(Run);}catch{watcher.Dispose();stop.Dispose();throw;}
 }
 void Overflow(){armed=false;watcher.EnableRaisingEvents=false;error(source.Name+"：监听事件溢出，已暂停。请重新启用；不会扫描补处理旧文件。");}
 void Queue(string kind,string path,string old){if(!armed)return;if(!events.Writer.TryWrite((kind,path,old)))Overflow();}
 async Task Run(){
  try{await foreach(var (kind,path,old) in events.Reader.ReadAllAsync(stop.Token)){
   if(kind=="deleted"){tracked.TryRemove(path,out _);vanished(path);continue;}
   if(kind=="renamed"){
    vanished(old);
    if(!tracked.TryRemove(old,out var task)||!Temporary(old))continue;
    if(!File.Exists(path))continue;
    try{FileIdentity.ValidatePath(path);try{var stamp=FileIdentity.Read(path);if(baseline.ContainsKey(stamp.Id))continue;}catch(System.ComponentModel.Win32Exception){}tracked[path]=(path,task.Seen);if(!Temporary(path))_=Check(path,task.Seen);}catch{}continue;
   }
   if(kind=="changed"){if(tracked.TryGetValue(path,out var entry)&&!Temporary(path))_=Check(path,entry.Seen);continue;}
   if(!File.Exists(path))continue;
   try{
    FileIdentity.ValidatePath(path);
    try{var stamp=FileIdentity.Read(path);if(baseline.ContainsKey(stamp.Id))continue;}catch(System.ComponentModel.Win32Exception){}catch(IOException){}
    // Created is the provenance. A downloader can still hold the file exclusively at this instant.
    var seen=DateTime.UtcNow;if(tracked.TryAdd(path,(path,seen))&&!Temporary(path))_=Check(path,seen);
   }catch{}
  }}catch(OperationCanceledException){}
 }
 readonly ConcurrentDictionary<string,byte> checking=new(StringComparer.OrdinalIgnoreCase);
 async Task Check(string path,DateTime seen) {
  if(!checking.TryAdd(path,0))return;
  try{
   var stamp=await DownloadCompletionDetector.WaitAsync(path,stop.Token);
   if(!stop.IsCancellationRequested&&stamp!=null&&tracked.TryRemove(path,out _)&&!baseline.ContainsKey(stamp.Id)){baseline.TryAdd(stamp.Id,0);ready(path,source,stamp,seen);}
  }catch(OperationCanceledException){}catch(Exception ex){error("completion_"+ex.GetType().Name);}finally{checking.TryRemove(path,out _);}
 }
 public void Dispose(){armed=false;watcher.Dispose();stop.Cancel();events.Writer.TryComplete();}
}
public static class DownloadCompletionDetector {
 public static async Task<Stamp?> WaitAsync(string path,CancellationToken ct) {
  if(SourceWatcher.Temporary(path))return null;
  for(int i=0;i<24;i++){
   ct.ThrowIfCancellationRequested();if(!File.Exists(path))return null;
   try{
    FileIdentity.ValidatePath(path);var a=FileIdentity.Read(path);
    await Task.Delay(180,ct);
    var b=FileIdentity.Read(path);if(a!=b)continue;
    await Task.Delay(180,ct);if(FileIdentity.Read(path)!=a)continue;
    using var lockTest=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None);
    if(FileIdentity.Read(lockTest.SafeFileHandle)!=a)continue;
    return a;
   }catch(IOException){}catch(System.ComponentModel.Win32Exception){}
   await Task.Delay(i<20?120:1000,ct);
  }return null; // Later write events retry this tracked file; no permanent polling.
 }
}
