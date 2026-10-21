using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using SmartFiles.Core;
namespace SmartFiles;
public partial class App:System.Windows.Application {
 Mutex? single;EventWaitHandle? quit;RegisteredWaitHandle? quitWait;EventWaitHandle? show;RegisteredWaitHandle? showWait;
 public SmartFilesEngine Engine {get;private set;}=null!;NotificationService? notifications;
 MainWindow? window;SourceWatcher? diagnosticWatcher;bool exiting;
 protected override async void OnStartup(StartupEventArgs e){
  base.OnStartup(e);System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  if(e.Args.Contains("--startup")){using(RuntimeHost.Launch("SmartFiles.Watcher.exe",RuntimeHost.Home(e.Args),"--startup")){}Shutdown();return;}
  if(e.Args.Contains("--exit")){RuntimeHost.StopAll(RuntimeHost.Home(e.Args));try{using var signal=EventWaitHandle.OpenExisting("Local\\SmartFiles.Exit");signal.Set();}catch{}Shutdown();return;}
  single=new Mutex(true,"Local\\SmartFiles.Desktop",out bool first);if(!first){var taskIndex=Array.IndexOf(e.Args,"--open-task");if(taskIndex>=0&&taskIndex+1<e.Args.Length){var inbox=Path.Combine(RuntimeHost.Home(e.Args),"Data","open-task.txt");File.WriteAllText(inbox,e.Args[taskIndex+1]);}try{using var signal=EventWaitHandle.OpenExisting("Local\\SmartFiles.Show");signal.Set();}catch{}single.Dispose();Shutdown();return;}
  quit=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\SmartFiles.Exit");
  quitWait=ThreadPool.RegisterWaitForSingleObject(quit,(_,_)=>Dispatcher.BeginInvoke(ExitApplication),null,-1,true);
  try{
   Directory.CreateDirectory(@"D:\SmartFilesApp\Temp");Environment.SetEnvironmentVariable("TEMP",@"D:\SmartFilesApp\Temp");Environment.SetEnvironmentVariable("TMP",@"D:\SmartFilesApp\Temp");
   bool uiSmoke=e.Args.Contains("--ui-smoke");bool discoverOnly=e.Args.Contains("--discover-only");
   string home=RuntimeHost.Home(e.Args);
   if(uiSmoke){
    string run=DateTime.Now.ToString("yyyyMMdd-HHmmss");home=Path.Combine(home,"TestData","UI-"+run,"home");
    var settings=new SettingsService(home);settings.Value.Root=Path.Combine(Path.GetDirectoryName(home)!,"tree");
    var source=Path.Combine(Path.GetDirectoryName(home)!,"source");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"old-before-monitor.txt"),"old fixture");
    settings.Value.Sources=[new(){Name="界面验收 · 新文件",Path=source,Enabled=true}];settings.Value.LocalAi=false;settings.Value.Notifications=false;settings.Save();
   }
   Engine=new(home,mode:uiSmoke?EngineMode.Legacy:EngineMode.UI);if(Engine.Settings.Value.AiDefaultsVersion<1){var defaults=Engine.Settings.Value;defaults.SendFilename=defaults.SendMetadata=defaults.SendText=defaults.SendThumbnail=true;if(DeepSeekProvider.IsDeepSeek(defaults.BaseUrl)){if(defaults.TextModel is "deepseek-chat" or "deepseek-reasoner")defaults.TextModel="deepseek-flash";if(string.IsNullOrWhiteSpace(defaults.VisionModel))defaults.VisionModel="deepseek-flash";}defaults.AiDefaultsVersion=1;Engine.Settings.Save();}show=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\SmartFiles.Show");showWait=ThreadPool.RegisterWaitForSingleObject(show,(_,_)=>Dispatcher.BeginInvoke(()=>{if(!exiting)OpenWindow();}),null,-1,false);if(Engine.Settings.Value.CloseToTray)notifications=new(Engine,OpenWindow,ExitApplication);if(!uiSmoke){RuntimeHost.EnsureWatcher(home);if(Engine.Settings.Value.AutoStart)Engine.Settings.SetStartup(true,Environment.ProcessPath!);}
   if(e.Args.Contains("--api-check")){
    try{var config=Engine.Settings.Value;var models=await Engine.Provider.ListModelsAsync(config.BaseUrl,Engine.Settings.ReadKey(),CancellationToken.None);var result=await Engine.Provider.TestAsync(CancellationToken.None);File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\live-api-check.json",Json.Encode(new{endpoint=config.BaseUrl,model=config.TextModel,models=models.Select(m=>m.Id).ToArray(),result,checkedUtc=DateTime.UtcNow}));}catch(Exception ex){File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\live-api-check.json",Json.Encode(new{result="测试未完成",errorType=ex.GetType().Name,checkedUtc=DateTime.UtcNow}));}ExitApplication();return;
   }
   Engine.Fault+=s=>Dispatcher.BeginInvoke(()=>{window?.ShowStatus(s);Engine.Log.Write("source_fault");});
   bool startup=e.Args.Contains("--startup"),idleTest=e.Args.Contains("--idle-test");
   if(!startup&&!idleTest&&!discoverOnly)OpenWindow();
   if(startup)await Task.Delay(20000);if(exiting)return;
   bool monitoring=Engine.Settings.Value.Monitoring;
   if(Engine.Settings.Value.Sources.Count==0)Engine.Discover();
   if(monitoring&&!idleTest)await Engine.StartAsync();
   window?.Refresh();
   if(idleTest)_=IdleTest();
   if(discoverOnly){Engine.Discover();ExitApplication();return;}
   if(uiSmoke){
    OpenWindow();await Engine.StartAsync();
    var gaps=new System.Collections.Generic.List<double>();var clock=Stopwatch.StartNew();double previous=clock.Elapsed.TotalMilliseconds;
    var heartbeat=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
    heartbeat.Tick+=(_,_)=>{double now=clock.Elapsed.TotalMilliseconds;gaps.Add(now-previous);previous=now;};heartbeat.Start();
    await Task.Run(()=>{for(int i=0;i<100;i++)File.Copy(@"D:\SmartFilesApp\TestData\AiFixtures\ocr.png",Path.Combine(Engine.Settings.Value.Sources[0].Path,$"Screenshot_{i:000}.png"));});
    for(int i=0;i<100&&Engine.Pending.Count(x=>x.Status=="待确认")<100;i++)await Task.Delay(100);
    heartbeat.Stop();window!.Refresh();await Task.Delay(250);await window.CapturePagesAsync();
    File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\ui-batch.json",Json.Encode(new{pending=Engine.Pending.Count,ready=Engine.Pending.Count(x=>x.Status=="待确认"),oldExcluded=!Engine.Pending.Any(x=>x.Name=="old-before-monitor.txt"),elapsedMs=clock.ElapsedMilliseconds,dispatcherMaxGapMs=gaps.Count>0?gaps.Max():0}));
    ExitApplication();
   }
  }catch(Exception ex){System.Windows.MessageBox.Show("SmartFiles 未启动。\n"+ex.Message,"SmartFiles");Shutdown();}
 }
 void OpenWindow(){
  if(window==null){window=new MainWindow(Engine);MainWindow=window;window.Closed+=(_,_)=>{window=null;MainWindow=null;if(!Engine.Settings.Value.CloseToTray)ExitApplication();};}
  window.Show();window.WindowState=WindowState.Normal;window.Activate();var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"--open-task");if(index>=0&&index+1<args.Length)window.OpenTask(args[index+1]);var inbox=Path.Combine(Engine.Settings.Home,"Data","open-task.txt");if(File.Exists(inbox)){var request=File.ReadAllText(inbox);File.Delete(inbox);window.OpenTask(request);}
 }
 async Task IdleTest(){
  try{
   var p=@"D:\SmartFilesApp\TestData\IdleWatch";Directory.CreateDirectory(p);
   diagnosticWatcher=new SourceWatcher(new(){Name="空闲验收",Path=p,Enabled=true},(_,_,_,_)=>{},_=>{},_=>{});
   await Task.Delay(10000);
   using var process=Process.GetCurrentProcess();var start=process.TotalProcessorTime;var timer=Stopwatch.StartNew();var samples=new System.Collections.Generic.List<double>();
   for(int i=0;i<60;i++){await Task.Delay(5000);process.Refresh();samples.Add(process.WorkingSet64/1048576d);}
   process.Refresh();var cpu=(process.TotalProcessorTime-start).TotalMilliseconds/timer.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100;
   var report=new{durationSeconds=timer.Elapsed.TotalSeconds,cpuPercent=cpu,workingSetAverageMB=samples.Average(),workingSetMaxMB=samples.Max(),logicalProcessors=Environment.ProcessorCount,watcherEnabled=true,mode="Release tray, real FileSystemWatcher, no main window, no GPU provider"};
   Directory.CreateDirectory(@"D:\SmartFilesApp\DevArtifacts");File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\idle-performance.json",Json.Encode(report));
  }catch(Exception ex){Engine.Log.Write("idle_test_failed",ex);}finally{ExitApplication();}
 }
 public async void ExitApplication(){
  if(exiting)return;exiting=true;showWait?.Unregister(null);show?.Dispose();quitWait?.Unregister(null);quit?.Dispose();notifications?.Dispose();diagnosticWatcher?.Dispose();
  if(Engine!=null)await Engine.DisposeAsync();single?.Dispose();Shutdown();
 }
}
