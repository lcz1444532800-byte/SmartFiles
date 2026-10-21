using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SmartFiles.Core;
namespace SmartFiles.Tests;
static class ApiTests {
 sealed class Server:IDisposable {
  readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly Func<string,Task<(int,string)>> handler;
  public string Url {get;} public List<string> Requests {get;}=[];public int MaxActive;int active;
  public Server(Func<string,Task<(int,string)>> h){handler=h;listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;_=Run();}
  async Task Run(){try{while(!stop.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(stop.Token);_=Process(client);}}catch(OperationCanceledException){}catch(SocketException){}}
  async Task Process(TcpClient client){using(client){try{
   using var stream=client.GetStream();var header=new List<byte>();var one=new byte[1];
   while(header.Count<65536){if(await stream.ReadAsync(one,stop.Token)==0)return;header.Add(one[0]);if(header.Count>=4&&Encoding.ASCII.GetString(header.TakeLast(4).ToArray())=="\r\n\r\n")break;}
   var headers=Encoding.ASCII.GetString(header.ToArray());var lengthHeader=headers.Split("\r\n").FirstOrDefault(x=>x.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase));int len=lengthHeader==null?0:int.Parse(lengthHeader.Split(':')[1].Trim());
   var body=new byte[len];await stream.ReadExactlyAsync(body,stop.Token);var json=Encoding.UTF8.GetString(body);lock(Requests)Requests.Add(json);
   var now=Interlocked.Increment(ref active);MaxActive=Math.Max(MaxActive,now);
   var (status,result)=await handler(json);Interlocked.Decrement(ref active);
   var bytes=Encoding.UTF8.GetBytes(result);var response=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
   await stream.WriteAsync(response,stop.Token);await stream.WriteAsync(bytes,stop.Token);
  }catch{}}}
  public void Dispose(){stop.Cancel();listener.Stop();}
 }
 static void Assert(bool x,string reason){if(!x)throw new Exception(reason);}
 static string Response(string id)=>Json.Encode(new{choices=new[]{new{message=new{content=Json.Encode(new{target_folder_id=id,confidence=.9})}}}});
 public static async Task Run(string root){
  Directory.CreateDirectory(root);var s=new SettingsService(root);s.Value.TextModel="test";s.Value.SendFilename=false;s.Value.SendMetadata=false;s.Value.SendText=false;s.SaveKey("synthetic-test-key-not-real");
  var nodes=new[]{new FolderNode("allowed",root,"Study")};
  using(var server=new Server(_=>Task.FromResult((200,"{\"data\":[{\"id\":\"text-model\",\"input_modalities\":[\"text\"]},{\"id\":\"vision-model\",\"input_modalities\":[\"text\",\"image\"]}]}")))){s.Value.BaseUrl=server.Url;s.SaveKey("synthetic-test-key-not-real");using var api=new DeepSeekProvider(s);var list=await api.ListModelsAsync(server.Url,s.ReadKey(),CancellationToken.None);Assert(list.Count==2&&list.Single(x=>x.Id=="vision-model").Vision,"model discovery capabilities failed");Assert(s.ReadKey("https://example.org/v1")=="","key leaked to another endpoint");}

  using(var server=new Server(async _=>{await Task.Delay(150);return(200,Response("allowed"));})){
   s.Value.BaseUrl=server.Url;s.SaveKey("synthetic-test-key-not-real");using var api=new DeepSeekProvider(s);
   var tasks=Enumerable.Range(0,6).Select(_=>api.SuggestAsync(new("SECRET_FILENAME",".txt",1,"SECRET_METADATA","SECRET_TEXT",false),nodes,null,CancellationToken.None));
   var result=await Task.WhenAll(tasks);
   Assert(result.All(x=>x?.FolderId=="allowed"),"valid reply rejected");
   Assert(server.MaxActive<=2&&server.MaxActive>0,"API concurrency exceeded");
   Assert(server.Requests.All(x=>!x.Contains("SECRET_")),"privacy fields leaked");
  }
  s.Value.ApiConcurrency=1;s.Value.SendFilename=s.Value.SendMetadata=s.Value.SendText=s.Value.SendThumbnail=true;s.Value.VisionModel="vision-model";
  using(var server=new Server(async _=>{await Task.Delay(50);return(200,Response("allowed"));})){s.Value.BaseUrl=server.Url;s.SaveKey("synthetic-test-key-not-real");using var api=new DeepSeekProvider(s);await Task.WhenAll(Enumerable.Range(0,3).Select(_=>api.SuggestAsync(new("VISIBLE_NAME",".png",1,"VISIBLE_META","VISIBLE_TEXT",true),nodes,new byte[]{1,2,3},CancellationToken.None)));Assert(server.MaxActive==1,"configured concurrency ignored");Assert(server.Requests.All(x=>x.Contains("VISIBLE_NAME")&&x.Contains("VISIBLE_META")&&x.Contains("VISIBLE_TEXT")&&x.Contains("image_url")),"enabled content missing");}
  using(var server=new Server(_=>Task.FromResult((200,Response("allowed"))))){s.Value.BaseUrl=server.Url;s.SaveKey("synthetic-test-key-not-real");using var api=new DeepSeekProvider(s);var evidence=new Evidence("page.png",".png",100,@"D:\Private\secret.pdf",new string('A',3000)+"TAIL_SECRET",true,true);await api.SuggestAsync(evidence,nodes,new byte[]{1,2,3},CancellationToken.None);var body=server.Requests.Single();Assert(!body.Contains("image_url")&&!body.Contains("TAIL_SECRET")&&!body.Contains("Private"),"OCR minimization / path redaction failed");}
  var payload=new Dictionary<string,object>();DeepSeekProvider.ConfigurePayload(payload,"https://api.deepseek.com");Assert(payload.ContainsKey("thinking"),"DeepSeek mode missing");payload.Clear();DeepSeekProvider.ConfigurePayload(payload,"https://generativelanguage.googleapis.com/v1beta/openai");Assert((string)payload["reasoning_effort"]=="low"&&!payload.ContainsKey("thinking"),"Gemini request incompatible");payload.Clear();DeepSeekProvider.ConfigurePayload(payload,"https://example.org/v1");Assert(payload.Count==0,"provider-specific options sent to custom API");
  using(var server=new Server(_=>Task.FromResult((200,Response("../../Windows/System32"))))){
   s.Value.BaseUrl=server.Url;s.SaveKey("synthetic-test-key-not-real");using var api=new DeepSeekProvider(s);
   Assert(await api.SuggestAsync(new("a",".txt",1,"","",false),nodes,null,CancellationToken.None)==null,"malicious path accepted");
  }
  using(var server=new Server(_=>Task.FromResult((500,"{}")))){
   s.Value.BaseUrl=server.Url;s.SaveKey("synthetic-test-key-not-real");using var api=new DeepSeekProvider(s);
   await api.SuggestAsync(new("a",".txt",1,"","",false),nodes,null,CancellationToken.None);
   await api.SuggestAsync(new("b",".txt",1,"","",false),nodes,null,CancellationToken.None);
   Assert(server.Requests.Count==2,"retry / circuit breaker unbounded");
  }
 }
 public static async Task Lifecycle(string root){
  var home=Path.Combine(root,"home");var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
  var settings=new SettingsService(home);settings.Value.Root=Path.Combine(root,"tree");settings.Value.LocalAi=false;settings.Value.InteractiveTtlMs=150;settings.Value.BatchWindowMs=500;
  settings.Value.Sources=[new(){Name="synthetic",Path=source,Enabled=true}];settings.SaveKey("synthetic-test-key-not-real");
  using var server=new Server(async body=>{
   await Task.Delay(900);using var json=JsonDocument.Parse(body);var content=json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
   using var data=JsonDocument.Parse(content);var id=data.RootElement.GetProperty("candidates")[0].GetProperty("folder_id").GetString()!;
   return(200,Response(id));
  });
  settings.Value.BaseUrl=server.Url;settings.Value.VisionModel="";settings.SaveKey("synthetic-test-key-not-real");settings.Save();
  await using var engine=new SmartFilesEngine(home);int notices=0;engine.Ready+=_=>Interlocked.Increment(ref notices);await engine.StartAsync();
  var gone=Path.Combine(source,"gone.txt");File.WriteAllText(gone,"untrusted text");await Task.Delay(400);File.Delete(gone);await Task.Delay(1400);
  Assert(engine.Pending.Count==0&&notices==0,"deleted file / late result notified");
  var path=Path.Combine(source,"new.txt");File.WriteAllText(path,"untrusted text");await Task.Delay(2000);
  Assert(engine.Pending.Count==1&&engine.Pending[0].Status=="Pending"&&notices==0,"TTL must queue silently");
  Assert(File.Exists(path),"moved without approval");
  var task=engine.Pending[0];await engine.ConfirmAsync(task,task.FolderId,task.Name,false);
  Assert(!File.Exists(path)&&engine.Pending.Count==0,"explicit confirm did not move");
  int before=server.Requests.Count;
  for(int i=0;i<100;i++)File.WriteAllBytes(Path.Combine(source,$"IMG_{i:000}.png"),Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1cAAAAASUVORK5CYII="));
  for(int i=0;i<150&&engine.Pending.Count(x=>x.Status=="Pending")<100;i++)await Task.Delay(100);
  Assert(engine.Pending.Count==100&&engine.Pending.All(x=>x.Status=="Pending"),"image batch did not settle");
  Assert(server.Requests.Count-before==settings.Value.ImageApiBudgetPerBatch,"100 images must obey per-batch API budget");
 }
}
