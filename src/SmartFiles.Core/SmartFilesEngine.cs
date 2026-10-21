using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
namespace SmartFiles.Core;
public enum EngineMode { Legacy, UI, Worker }
public sealed class SmartFilesEngine : IAsyncDisposable {
 public SettingsService Settings {get;} public HistoryService History {get;} public TaxonomyService Taxonomy {get;}
 public RuleEngine Rules {get;} public FileTransactionService Transactions {get;} public UndoService Undo {get;}
 public DeepSeekProvider Provider {get;} public SafeLog Log {get;}
 readonly EngineMode mode;readonly FileSystemWatcher? resultWatch;
 readonly List<SourceWatcher> watchers=[];readonly CancellationTokenSource stop=new();
 readonly Channel<FileTask> queue=Channel.CreateBounded<FileTask>(4096);
 readonly ConcurrentDictionary<string,FileTask> pending=new();readonly ConcurrentDictionary<string,CancellationTokenSource> active=new();
 readonly ConcurrentDictionary<string,byte> admitted=new();
 readonly ConcurrentDictionary<string,int> imageBudget=new();
 readonly AIWorkerClient worker;readonly IOcrProvider ocr;
 readonly MetadataExtractor metadata=new();readonly LocalClassifier classifier;readonly CandidateSelector selector=new();
 readonly ConcurrentDictionary<string,Task> remote=new();readonly Task consumer;readonly object batchGate=new();
 string batch=Guid.NewGuid().ToString("N");DateTime lastSeen=DateTime.MinValue;
 public event Action? Updated;public event Action<FileTask>? Ready;public event Action<string>? Fault;
 public List<FileTask> Pending{get{if(mode==EngineMode.UI)ReloadPending();return pending.Values.OrderByDescending(x=>x.SeenUtc).ToList();}}
 public SmartFilesEngine(string home,bool initialize=true,EngineMode mode=EngineMode.Legacy) {
  this.mode=mode;
  Settings=new(home);History=new(home);History.ConfigureRetention(Settings.Value.HistoryMaxCount,Settings.Value.HistoryMaxDays);Log=new(home);Taxonomy=new(Settings.Value.Root,History,initialize);
  Rules=new(History);Transactions=new(History,home,Settings.Value.Root);Undo=new(Transactions,History);Provider=new(Settings);
  worker=new(RuntimeHost.DefaultHome){IdleSeconds=Settings.Value.WorkerIdleSeconds};ocr=new ExistingOcrProvider(worker);classifier=new(Rules);
  if(mode!=EngineMode.Worker)Transactions.Recover();
  StorageMaintenance.Run(home,History,Settings.Value);if(mode!=EngineMode.Worker)ReloadPending();consumer=mode==EngineMode.UI?Task.CompletedTask:Task.Run(Consume);
  if(mode==EngineMode.UI){resultWatch=new(Path.Combine(home,"Data")){NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName,IncludeSubdirectories=true};resultWatch.Changed+=(_,_)=>Updated?.Invoke();resultWatch.Created+=(_,_)=>Updated?.Invoke();resultWatch.Renamed+=(_,_)=>Updated?.Invoke();resultWatch.EnableRaisingEvents=true;}
 }
 void ReloadPending(){
  if(mode==EngineMode.UI)foreach(var path in Directory.EnumerateFiles(RuntimeHost.Queue(Settings.Home),"*.manual"))try{var job=RuntimeHost.ReadJob(path);if(History.Count("pending")>=10000)break;if(FileIdentity.Matches(job.Task.Path,job.Task.Stamp))History.Put("pending",job.Task.Id,job.Task);File.Delete(path);}catch{}
  var rows=History.Read<FileTask>("pending");var ids=rows.Select(x=>x.Id).ToHashSet();foreach(var id in pending.Keys.Where(x=>!ids.Contains(x)))pending.TryRemove(id,out _);
  foreach(var task in rows){if(!File.Exists(task.Path)){History.Delete("pending",task.Id);pending.TryRemove(task.Id,out _);continue;}if(task.Features.Length>256||task.Features!=PrivacyBoundary.Text(task.Name,256)){task.Features=PrivacyBoundary.Text(task.Name,256);History.Put("pending",task.Id,task);}pending[task.Id]=task;admitted[task.Stamp.Id]=0;}
 }
 public async Task ProcessExternalAsync(FileTask task,CancellationToken ct){Settings.Reload();History.ConfigureRetention(Settings.Value.HistoryMaxCount,Settings.Value.HistoryMaxDays);worker.IdleSeconds=Settings.Value.WorkerIdleSeconds;pending[task.Id]=task;admitted[task.Stamp.Id]=0;task.Status="分析中";History.Put("pending",task.Id,task);queue.Writer.TryWrite(task);while(pending.TryGetValue(task.Id,out var current)&&current.Status=="分析中"){await Task.Delay(50,ct);}pending.TryRemove(task.Id,out _);admitted.TryRemove(task.Stamp.Id,out _); }
 public List<Source> Discover(){
  var known=Settings.Value.Sources;
  foreach(var s in new SourceDiscovery().Discover(Settings.Value.Root))if(!known.Any(x=>string.Equals(x.Path,s.Path,StringComparison.OrdinalIgnoreCase)))known.Add(s);
  Settings.Save();return known;
 }
 public async Task StartAsync(){
  if(mode==EngineMode.UI){Settings.Value.Monitoring=true;Settings.Save();RuntimeHost.EnsureWatcher(Settings.Home);return;}
  StopMonitoring();
  foreach(var s in Settings.Value.Sources.Where(x=>x.Enabled&&x.Policy!=SourcePolicy.Ignore)){
   try{
    if(FileIdentity.Under(s.Path,Settings.Home)||string.Equals(s.Path,Path.GetPathRoot(s.Path),StringComparison.OrdinalIgnoreCase))throw new IOException("禁止监控程序目录或整盘");
    if((string.Equals(Path.GetFullPath(s.Path),Settings.Value.Root,StringComparison.OrdinalIgnoreCase)||FileIdentity.Under(s.Path,Settings.Value.Root))&&!string.Equals(Path.GetFullPath(s.Path),Path.Combine(Settings.Value.Root,"_Inbox"),StringComparison.OrdinalIgnoreCase))throw new IOException("只允许监控分类根目录中的 _Inbox");
    var watcher=await Task.Run(()=>new SourceWatcher(s,Accept,Cancel,FaultMessage));watchers.Add(watcher);
   }catch(Exception e){Log.Write("source_start_failed",e);FaultMessage(s.Name+" 无法监听："+e.Message);}
  }
  Settings.Value.Monitoring=watchers.Count>0;Settings.Save();Updated?.Invoke();
 }
 void FaultMessage(string s)=>Fault?.Invoke(s);
 public void StopMonitoring(){if(mode==EngineMode.UI){Settings.Value.Monitoring=false;Settings.Save();RuntimeHost.Signal(Settings.Home,"Watcher.Reload");return;}foreach(var w in watchers)w.Dispose();watchers.Clear();Settings.Value.Monitoring=false;}
 void Accept(string path,Source source,Stamp stamp,DateTime seen){
  if(!Settings.Value.Classification||stop.IsCancellationRequested||!admitted.TryAdd(stamp.Id,0))return;
  string batchId;lock(batchGate){if((seen-lastSeen).TotalMilliseconds>Settings.Value.BatchWindowMs){batch=Guid.NewGuid().ToString("N");if(imageBudget.Count>100)imageBudget.Clear();}lastSeen=seen;batchId=batch;}
  var task=new FileTask{Path=path,SourcePath=source.Path,SourceName=source.Name,Stamp=stamp,SeenUtc=seen,Status="分析中",BatchId=batchId};
  pending[task.Id]=task;History.Put("pending",task.Id,task);
  if(!queue.Writer.TryWrite(task)){task.Status="待确认";task.Note="队列已满，未进行 AI 分析";Save(task);}
  Updated?.Invoke();
 }
 void Cancel(string path){
  foreach(var task in pending.Values.Where(x=>string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase))){
   if(active.TryGetValue(task.Id,out var c))c.Cancel();
   pending.TryRemove(task.Id,out _);History.Delete("pending",task.Id);
  }Updated?.Invoke();
 }
 void Save(FileTask task){if(pending.ContainsKey(task.Id)){History.Put("pending",task.Id,task);Updated?.Invoke();}}
 bool Valid(FileTask task)=>pending.ContainsKey(task.Id)&&FileIdentity.Matches(task.Path,task.Stamp);
 async Task Consume(){
  try{await foreach(var task in queue.Reader.ReadAllAsync(stop.Token)){
   var cancellation=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);active[task.Id]=cancellation;var ct=cancellation.Token;
   try{
    if(!Valid(task)){Cancel(task.Path);active.TryRemove(task.Id,out _);cancellation.Dispose();continue;}
    var evidence=metadata.Extract(task.Path);var nodes=Taxonomy.Nodes.ToArray();
    var recommendation=classifier.Classify(evidence,task.SourceName,nodes);
    Dictionary<string,double>? semantic=null;
    if(recommendation.Confidence<Settings.Value.LocalThreshold&&Settings.Value.LocalAi){
     if(evidence.Image&&Settings.Value.Ocr)try{var text=await ocr.ReadAsync(task.Path,ct);if(text.Length>0)evidence=evidence with{Text=text,OcrDerived=true};}catch(Exception e) when(e is not OperationCanceledException){task.Note="OCR 不可用，已降级";}
     try{
      var result=await worker.RequestAsync(new{op="route",text=evidence.Name+" "+evidence.Metadata+" "+evidence.Text,nodes=nodes.Where(n=>!n.Label.StartsWith("_")).Select(n=>new{id=n.Id,text=n.Label+" "+n.Description+" "+n.Keywords})},ct);
      semantic=result.GetProperty("scores").Deserialize<Dictionary<string,double>>();
      // Similarity is a ranking score, not a calibrated probability; never promote alone to 95%.
      recommendation=classifier.Classify(evidence,task.SourceName,nodes);
      if(semantic is {Count:>0}&&recommendation.Confidence<.75){
       var best=semantic.OrderByDescending(x=>x.Value).Take(2).ToArray();
       if(best.Length==2&&best[0].Value>.75&&best[0].Value-best[1].Value>.06)recommendation=new(best[0].Key,.78,"OCR / BGE候选");
      }
     }catch(Exception e) when(e is not OperationCanceledException){task.Note="本地语义暂不可用，保留规则结果";}
    }
    if(evidence.Image&&evidence.Text.Length<24&&recommendation.Confidence<Settings.Value.LocalThreshold&&Settings.Value.LocalAi){try{var v=await new ImageAnalyzer(worker).AnalyzeAsync(task.Path,evidence,nodes,ct);evidence=v.Evidence;if(v.Suggestion!=null)recommendation=v.Suggestion;}catch(Exception ex) when(ex is not OperationCanceledException){task.Note+=" 本地视觉暂不可用";}}
    var rankingText=evidence.Name+" "+evidence.Metadata+" "+evidence.Text;task.Features=PrivacyBoundary.Text(evidence.Name,256);evidence=evidence with{LocalConfidence=recommendation.Confidence};
    Apply(task,recommendation);
    var candidates=selector.Select(rankingText,nodes,Settings.Value.CandidateCount,semantic);
    if(recommendation.Confidence<Settings.Value.LocalThreshold){
     if(evidence.Image&&imageBudget.AddOrUpdate(task.BatchId,1,(_,n)=>n+1)>Settings.Value.ImageApiBudgetPerBatch){
      task.Note+=" 批次视觉 API 额度已用完，等待人工确认";Finish(task);cancellation.Dispose();active.TryRemove(task.Id,out _);
     }else {var remoteTask=Remote(task,evidence,candidates,cancellation);remote[task.Id]=remoteTask;_ = remoteTask.ContinueWith(completed=>{remote.TryRemove(task.Id,out var ignored);},TaskScheduler.Default);}
    }else {Finish(task);cancellation.Dispose();active.TryRemove(task.Id,out _);}
   }catch(OperationCanceledException){active.TryRemove(task.Id,out _);cancellation.Dispose();}
    catch(Exception e){Log.Write("classification_failed",e);task.Note="分析失败，可手动选择分类";Finish(task);active.TryRemove(task.Id,out _);cancellation.Dispose();}
  }}catch(OperationCanceledException){}
 }
 void Apply(FileTask task,Recommendation r){
  task.FolderId=r.FolderId;task.TargetLabel=Taxonomy.Nodes.FirstOrDefault(n=>n.Id==r.FolderId)?.Label??"_待确认";task.Confidence=r.Confidence;task.ConfidenceBand=r.Confidence>=Settings.Value.HighThreshold?"较确定":r.Confidence>=Settings.Value.LocalThreshold?"可能":"不确定";task.Method=r.Method;task.BranchName=r.Branch;task.SuggestedName=r.Name;
 }
 async Task Remote(FileTask task,Evidence e,List<FolderNode> candidates,CancellationTokenSource cancellation){
  try{
   if(!Valid(task))return;
   byte[]? thumbnail=null;
   if(PrivacyBoundary.NeedsThumbnail(e)&&Settings.Value.SendThumbnail&&!string.IsNullOrWhiteSpace(Settings.Value.VisionModel))thumbnail=MetadataExtractor.Thumbnail(task.Path);
   var result=await Provider.SuggestAsync(e,candidates,thumbnail,cancellation.Token);
   if(result!=null&&Valid(task))Apply(task,result);
   else if(task.Confidence<Settings.Value.LocalThreshold)task.Note+=" 信息不足或 API 未启用，请人工检查";
   if(Valid(task))Finish(task);else Cancel(task.Path);
  }catch(OperationCanceledException){}catch(Exception ex){Log.Write("remote_failed",ex);if(Valid(task))Finish(task);}
  finally{active.TryRemove(task.Id,out _);cancellation.Dispose();}
 }
 void Finish(FileTask task){
  if(!Valid(task)){Cancel(task.Path);return;}task.Status="待确认";Save(task);
  if((DateTime.UtcNow-task.SeenUtc).TotalMilliseconds<=Settings.Value.InteractiveTtlMs)Ready?.Invoke(task);
 }
 public async Task ConfirmAsync(FileTask task,string folderId,string filename,bool branchApproved){
  if(stop.IsCancellationRequested)throw new IOException("程序正在退出");
  if(task.Status!="待确认"||!Valid(task)||!admitted.ContainsKey(task.Stamp.Id))throw new IOException("任务已失效，禁止处理未知或旧文件");
  var source=Settings.Value.Sources.FirstOrDefault(s=>string.Equals(s.Path,task.SourcePath,StringComparison.OrdinalIgnoreCase))??throw new IOException("来源已移除");
  if(!source.Enabled||source.Policy==SourcePolicy.Ignore)throw new IOException("该来源未启用");
  var folder=Taxonomy.Resolve(folderId);
  if(source.Policy==SourcePolicy.AskBeforeMove&&!source.WarningAcknowledged)throw new IOException("请先确认来源路径警告");
  if(task.BranchName.Length>0&&branchApproved)folder=Taxonomy.Add(folderId,task.BranchName);
  FileIdentity.SafeName(filename);
  if(!string.Equals(Path.GetExtension(filename),Path.GetExtension(task.Path),StringComparison.OrdinalIgnoreCase))throw new IOException("重命名不得更改扩展名");
  if(source.Policy!=SourcePolicy.ClassifyOnly){
   task.Status="整理中";Save(task);
   try{Transactions.StrictHash=Settings.Value.StrictHash;await Transactions.MoveAsync(task.Path,Path.Combine(folder.Path,filename),task.Stamp,task.Method);}
   catch{task.Status="待确认";Save(task);throw;}
  }
  Rules.Learn(task.Features,folder.Id);pending.TryRemove(task.Id,out _);History.Delete("pending",task.Id);Updated?.Invoke();
 }
 public void Dismiss(FileTask task){if(active.TryGetValue(task.Id,out var c))c.Cancel();pending.TryRemove(task.Id,out _);History.Delete("pending",task.Id);Updated?.Invoke();}
 public async ValueTask DisposeAsync(){
  resultWatch?.Dispose();if(mode!=EngineMode.UI)StopMonitoring();stop.Cancel();queue.Writer.TryComplete();worker.Dispose();
  await consumer;await Task.WhenAll(remote.Values.ToArray());
  await Transactions.WaitForIdleAsync();Provider.Dispose();Taxonomy.Dispose();StorageMaintenance.Run(Settings.Home,History,Settings.Value);History.Dispose();stop.Dispose();
 }
}
