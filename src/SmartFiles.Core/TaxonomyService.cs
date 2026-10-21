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
   foreach(var relative in new[]{"_Inbox","_Review","Study/Course Materials","Study/Assignments","Study/Papers and References","Study/Ebooks","Study/Exam Materials","AI and Development/AI Models","AI and Development/Workflows","AI and Development/Code and Projects","AI and Development/Tutorials and Documentation","AI and Development/Errors and Debugging","Software/Installers","Software/Drivers","Software/Tools","Software/Archives","Images and Assets/Screenshots","Images and Assets/Photos","Images and Assets/Chat Images","Images and Assets/Design Assets","Media/Video","Media/Screen Recordings","Media/Audio","Office Documents/Spreadsheets","Office Documents/Presentations","Office Documents/Forms and References","Other"})Directory.CreateDirectory(Path.Combine(root,relative));
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
 public FolderNode Resolve(string id){var n=Nodes.FirstOrDefault(x=>x.Id==id)??throw new IOException("Category does not exist");FileIdentity.ValidatePath(n.Path);if(!FileIdentity.Under(n.Path,root)||!Directory.Exists(n.Path))throw new IOException("Invalid category path");using var h=FileIdentity.Open(n.Path,false,true);if(FileIdentity.Read(h).Id!=n.Id)throw new IOException("Category folder identity changed");return n;}
 public FolderNode Add(string parentId,string name){
  FileIdentity.SafeName(name);var parent=Resolve(parentId);var p=Path.Combine(parent.Path,name);
  if(Path.GetRelativePath(root,p).Split('\\').Length>16)throw new IOException("Categories cannot exceed 16 levels");
  using var pin=FileIdentity.PinParents(p);if(Directory.Exists(p)||File.Exists(p))throw new IOException("A category with this name already exists");Directory.CreateDirectory(p);Refresh();return Nodes.Single(x=>x.Path==p);
 }
 public void Rename(string id,string name){var n=Resolve(id);FileIdentity.SafeName(name);var p=Path.Combine(Path.GetDirectoryName(n.Path)!,name);if(File.Exists(p)||Directory.Exists(p))throw new IOException("A folder with this name already exists");Directory.Move(n.Path,p);Refresh();}
 public void DeleteEmpty(string id){var n=Resolve(id);if(n.Label.StartsWith("_"))throw new IOException("Reserved category");Directory.Delete(n.Path,false);Refresh();}
 public void Describe(string id,string description,string keywords){var n=Resolve(id) with{Description=description,Keywords=keywords};history.Put("folder",id,n);Refresh();}
 public void Dispose(){watcher.Dispose();debounce.Dispose();}
}
