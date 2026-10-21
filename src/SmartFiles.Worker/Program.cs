using SmartFiles.Core;
namespace SmartFiles.Worker;
static class Program {
 [STAThread] static async Task<int> Main(string[] args){var home=RuntimeHost.Home(args);if(args.Contains("--toast-test")){try{SystemToast.Show(new(){Path="synthetic-notification.exe",TargetLabel="软件/安装包",SeenUtc=DateTime.UtcNow},home);}catch(Exception ex){new SafeLog(home).Write("toast_test_failed",ex);}return 0;}using var single=new Mutex(true,RuntimeHost.Name(home,"Worker.Singleton"),out var first);if(!first){RuntimeHost.Signal(home,"Worker.Wake");return 0;}
  using var quit=new EventWaitHandle(false,EventResetMode.ManualReset,RuntimeHost.Name(home,"Worker.Exit"));using var wake=new EventWaitHandle(false,EventResetMode.AutoReset,RuntimeHost.Name(home,"Worker.Wake"));using var stop=new CancellationTokenSource();var registration=ThreadPool.RegisterWaitForSingleObject(quit,(_,_)=>stop.Cancel(),null,-1,true);
  var queue=RuntimeHost.Queue(home);using var watch=new FileSystemWatcher(queue,"*.job"){NotifyFilter=NotifyFilters.FileName};watch.Created+=(_,_)=>wake.Set();watch.Renamed+=(_,_)=>wake.Set();watch.EnableRaisingEvents=true;
  try{await using var engine=new SmartFilesEngine(home,mode:EngineMode.Worker);var notified=new HashSet<string>();engine.Ready+=task=>{if(engine.Settings.Value.Notifications&&notified.Add(task.BatchId))SystemToast.Show(task,home);};
   while(!stop.IsCancellationRequested){var jobs=Directory.GetFiles(queue,"*.job");foreach(var path in jobs){if(stop.IsCancellationRequested)break;WorkItem job;try{job=RuntimeHost.ReadJob(path);}catch{try{File.Move(path,Path.ChangeExtension(path,"invalid"),true);}catch{}new SafeLog(home).Write("invalid_queue_record");continue;}if(job.Attempts>=3){RuntimeHost.Manual(path,"此任务已失败三次，请人工选择分类");continue;}if(!FileIdentity.Matches(job.Task.Path,job.Task.Stamp)){File.Delete(path);continue;}
    var existing=engine.History.Find<FileTask>("pending",job.Task.Id);if(existing?.Status=="待确认"){File.Delete(path);continue;}
    if(existing==null&&engine.History.Count("pending")>=10000){RuntimeHost.Manual(path,"待确认队列已满，请先处理已有任务再恢复监听");engine.Settings.Value.Monitoring=false;engine.Settings.Save();continue;}
    job.Attempts++;RuntimeHost.WriteJob(path,job);await engine.ProcessExternalAsync(job.Task,stop.Token);if(!stop.IsCancellationRequested)File.Delete(path);
   }
   if(stop.IsCancellationRequested)break;if(Directory.EnumerateFiles(queue,"*.job").Any())continue;
   int idle=Math.Clamp(engine.Settings.Value.WorkerIdleSeconds,30,600);int signaled=await Task.Run(()=>WaitHandle.WaitAny([quit,wake],TimeSpan.FromSeconds(idle)));if(signaled!=1)break;engine.Settings.Reload();
  }return 0;}catch(OperationCanceledException){return 0;}catch(Exception ex){new SafeLog(home).Write("worker_failed",ex);return 1;}finally{registration.Unregister(null);}
 }
}
