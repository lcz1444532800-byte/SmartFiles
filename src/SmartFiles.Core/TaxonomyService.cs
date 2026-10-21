using System.Security.Cryptography;
using System.Text;
namespace SmartFiles.Core;
public sealed class TaxonomyService : IDisposable {
 readonly string root;readonly HistoryService history;readonly FileSystemWatcher watcher;readonly System.Threading.Timer debounce;
 public event Action? Changed;
 public List<FolderNode> Nodes {get;private set;}=[];
 public TaxonomyService(string path,HistoryService db,bool defaults=true){
  root=Path.GetFullPath(path);history=db;FileIdentity.ValidatePath(root);Directory.CreateDirectory(root);
  if(defaults&&!history.Read<string>("meta").Contains("initialized")){
   foreach(var relative in new[]{"_Inbox","_待确认","学习/课程资料","学习/作业","学习/论文与文献","学习/电子书","学习/考试资料","AI与开发/AI模型","AI与开发/工作流","AI与开发/代码与项目","AI与开发/教程与文档","AI与开发/报错与调试","软件/安装包","软件/驱动","软件/工具","软件/压缩包","图片与素材/截图","图片与素材/照片","图片与素材/聊天图片","图片与素材/设计素材","媒体/视频","媒体/录屏","媒体/音频","办公文档/表格","办公文档/演示","办公文档/表单与资料","其他"})Directory.CreateDirectory(Path.Combine(root,relative));
   history.Put("meta","initialized","initialized");
  }
  Refresh();debounce=new(_=>{try{Refresh();Changed?.Invoke();}catch{}},null,Timeout.Infinite,Timeout.Infinite);
  watcher=new(root){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.DirectoryName};
  watcher.Created+=(_,_)=>Schedule();watcher.Deleted+=(_,_)=>Schedule();watcher.Renamed+=(_,_)=>Schedule();watcher.Error+=(_,_)=>Schedule();watcher.EnableRaisingEvents=true;
 }
 void Schedule()=>debounce.Change(250,Timeout.Infinite);
 public void Refresh(){
  var result=new List<FolderNode>();var saved=history.Read<FolderNode>("folder").ToDictionary(x=>x.Id);var queue=new Queue<string>();queue.Enqueue(root);
  while(queue.TryDequeue(out var p))foreach(var dir in Directory.EnumerateDirectories(p)){
   if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;
   var label=Path.GetRelativePath(root,dir);if(label=="_Inbox")continue;
   if(label.Split('\\').Length>16)continue;
   using var h=FileIdentity.Open(dir,false,true);var id=FileIdentity.Read(h).Id;
   var node=new FolderNode(id,dir,label,saved.GetValueOrDefault(id)?.Description??"",saved.GetValueOrDefault(id)?.Keywords??"");
   result.Add(node);history.Put("folder",id,node);queue.Enqueue(dir);
  }
  Nodes=result.OrderBy(x=>x.Label).ToList();
 }
 public FolderNode Resolve(string id){var n=Nodes.FirstOrDefault(x=>x.Id==id)??throw new IOException("分类不存在");FileIdentity.ValidatePath(n.Path);if(!FileIdentity.Under(n.Path,root)||!Directory.Exists(n.Path))throw new IOException("分类路径无效");using var h=FileIdentity.Open(n.Path,false,true);if(FileIdentity.Read(h).Id!=n.Id)throw new IOException("分类目录身份已变化");return n;}
 public FolderNode Add(string parentId,string name){
  FileIdentity.SafeName(name);var parent=Resolve(parentId);var p=Path.Combine(parent.Path,name);
  if(Path.GetRelativePath(root,p).Split('\\').Length>16)throw new IOException("分类层数上限 16");
  using var pin=FileIdentity.PinParents(p);if(Directory.Exists(p)||File.Exists(p))throw new IOException("同名分类已存在");Directory.CreateDirectory(p);Refresh();return Nodes.Single(x=>x.Path==p);
 }
 public void Rename(string id,string name){var n=Resolve(id);FileIdentity.SafeName(name);var p=Path.Combine(Path.GetDirectoryName(n.Path)!,name);if(File.Exists(p)||Directory.Exists(p))throw new IOException("同名目录已存在");Directory.Move(n.Path,p);Refresh();}
 public void DeleteEmpty(string id){var n=Resolve(id);if(n.Label.StartsWith("_"))throw new IOException("保留分类");Directory.Delete(n.Path,false);Refresh();}
 public void Describe(string id,string description,string keywords){var n=Resolve(id) with{Description=description,Keywords=keywords};history.Put("folder",id,n);Refresh();}
 public void Dispose(){watcher.Dispose();debounce.Dispose();}
}
