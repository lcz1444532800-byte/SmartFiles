using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using SmartFiles.Core;
namespace SmartFiles.Tests;
static class PerfCore {
 public static async Task Run(string root){
  Directory.CreateDirectory(root);using var history=new HistoryService(root);using var taxonomy=new TaxonomyService(Path.Combine(root,"tree"),history);
  var classify=new LocalClassifier(new RuleEngine(history));var extract=new MetadataExtractor();var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
  var timestamps=new ConcurrentDictionary<string,long>();var detection=new ConcurrentBag<double>();var latency=new ConcurrentBag<double>();var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  using var watcher=new SourceWatcher(new(){Name="Latency fixture",Path=source,Enabled=true},(path,_,stamp,seen)=>{
   detection.Add(Stopwatch.GetElapsedTime(timestamps[path]).TotalMilliseconds);
   var start=Stopwatch.GetTimestamp();var result=classify.Classify(extract.Extract(path),"test",taxonomy.Nodes);latency.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
   if(latency.Count==20)completed.TrySetResult();
  },_=>{},error=>completed.TrySetException(new Exception(error)));
  for(int i=0;i<20;i++){var p=Path.Combine(source,$"model-{i}.safetensors");timestamps[p]=Stopwatch.GetTimestamp();File.WriteAllText(p,"synthetic-only");await Task.Delay(25);}
  await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
  var report=new{sampleCount=latency.Count,completionAverageMs=detection.Average(),completionMaxMs=detection.Max(),localRecommendationAverageMs=latency.Average(),localRecommendationMaxMs=latency.Max(),method="Created to ready: three identity/size samples plus exclusive lock; then MetadataExtractor and LocalClassifier"};
  File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\core-performance.json",Json.Encode(report));Console.WriteLine(Json.Encode(report));
 }
}
