using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
namespace SmartFiles.AIWorker;
static class Program {
 public static readonly string Home=@"D:\SmartFilesApp";
 public static SessionOptions Options(){var s=new SessionOptions{IntraOpNumThreads=1,InterOpNumThreads=1,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,EnableMemoryPattern=false,EnableCpuMemArena=false};s.AddSessionConfigEntry("session.intra_op.allow_spinning","0");s.AddSessionConfigEntry("session.inter_op.allow_spinning","0");return s;}
 [STAThread] static async Task Main(){
  Console.InputEncoding=Encoding.UTF8;Console.OutputEncoding=new UTF8Encoding(false);BgeEmbedding? bge=null;ExistingOcr? ocr=null;ILocalVisionProvider? vision=null;
  var cached=new Dictionary<string,float[]>();var cachePath=Path.Combine(Home,"Cache","node-embeddings.json");
  if(File.Exists(cachePath))try{cached=JsonSerializer.Deserialize<Dictionary<string,float[]>>(File.ReadAllText(cachePath))??[];}catch{}
  try{while(true){
   var read=Task.Run(Console.ReadLine);if(await Task.WhenAny(read,Task.Delay(Math.Clamp(int.TryParse(Environment.GetEnvironmentVariable("SMARTFILES_AI_IDLE_SECONDS"),out var idle)?idle:45,30,600)*1000))!=read)break;
   var line=await read;if(line==null)break;
   var timer=Stopwatch.StartNew();object result;
   try{
    using var doc=JsonDocument.Parse(line);var request=doc.RootElement;var op=request.GetProperty("op").GetString();
    if(op=="embed"){bge??=new(Path.Combine(Home,"Models","TextEmbedding"));result=new{embedding=bge.Encode(request.GetProperty("text").GetString()??"")};}
    else if(op=="route"){
     bge??=new(Path.Combine(Home,"Models","TextEmbedding"));var query=bge.Encode(request.GetProperty("text").GetString()??"");var scores=new Dictionary<string,double>();bool dirty=false;
     foreach(var n in request.GetProperty("nodes").EnumerateArray()){
      var id=n.GetProperty("id").GetString()!;var text=n.GetProperty("text").GetString()!;var key=id+"|"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
      if(!cached.TryGetValue(key,out var vector)){vector=bge.Encode(text);cached[key]=vector;dirty=true;}
      scores[id]=query.Zip(vector,(x,y)=>(double)x*y).Sum();
     }
     if(dirty){Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);if(cached.Count>2048)cached=cached.TakeLast(1024).ToDictionary();File.WriteAllText(cachePath+".new",JsonSerializer.Serialize(cached));File.Move(cachePath+".new",cachePath,true);}
     result=new{scores,ms=timer.ElapsedMilliseconds};
    }
    else if(op=="ocr"){
     ocr??=new(@"D:\DeepPickAI\models");var path=request.GetProperty("path").GetString()!;
     result=new{text=ocr.Read(path),ms=timer.ElapsedMilliseconds};
    }
    else if(op=="vision"){vision??=new TinyClipVision(Path.Combine(Home,"Models","Vision"));var v=vision.Analyze(request.GetProperty("path").GetString()!);result=new{label=v.Label,score=v.Score,margin=v.Margin,scores=v.Scores,ms=timer.ElapsedMilliseconds};}
    else result=new{error="unknown_operation"};
   }catch(Exception e){result=new{error=e.GetType().Name};}
   Console.WriteLine(JsonSerializer.Serialize(result));Console.Out.Flush();
  }}finally{ocr?.Dispose();bge?.Dispose();vision?.Dispose();}
 }
}
