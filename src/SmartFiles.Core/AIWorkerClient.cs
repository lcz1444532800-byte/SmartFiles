using System.Diagnostics;
using System.Text.Json;
namespace SmartFiles.Core;
public interface IOcrProvider { Task<string> ReadAsync(string path,CancellationToken ct); }
public sealed class ExistingOcrProvider(AIWorkerClient worker):IOcrProvider {
 public async Task<string> ReadAsync(string path,CancellationToken ct)=>(await worker.RequestAsync(new{op="ocr",path},ct)).GetProperty("text").GetString()??"";
}
public sealed class AIWorkerClient(string home):IDisposable {
 ChildJob? children;public int IdleSeconds {get;set;}=45;Process? process;readonly SemaphoreSlim gate=new(1,1);
 public async Task<JsonElement> RequestAsync(object request,CancellationToken ct){
  await gate.WaitAsync(ct);
  try{
   if(process==null||process.HasExited){
    process?.Dispose();var exe=Path.Combine(home,"Release","AIWorker","SmartFiles.AIWorker.exe");
    var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=home};
    start.Environment["TEMP"]=Path.Combine(home,"Temp");start.Environment["TMP"]=Path.Combine(home,"Temp");
    start.Environment["OMP_NUM_THREADS"]="1";start.Environment["SMARTFILES_AI_IDLE_SECONDS"]=IdleSeconds.ToString();
    process=Process.Start(start)??throw new IOException("无法启动 AIWorker");
    children??=new ChildJob();children.Add(process);process.ErrorDataReceived+=(_,_)=>{};process.BeginErrorReadLine();
   }
   using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromSeconds(45));
   await process.StandardInput.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(request).AsMemory(),limit.Token);
   var line=await process.StandardOutput.ReadLineAsync(limit.Token)??throw new IOException("AIWorker 已退出");
   using var json=JsonDocument.Parse(line);if(json.RootElement.TryGetProperty("error",out _))throw new IOException("本地 AI 暂不可用");return json.RootElement.Clone();
  }catch{if(process is {HasExited:false})process.Kill(true);throw;}finally{gate.Release();}
 }
 public void Dispose(){if(process is {HasExited:false}){try{process.Kill(true);}catch{}}process?.Dispose();children?.Dispose();}
}
