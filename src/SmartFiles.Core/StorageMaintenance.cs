using System.Runtime.InteropServices;
namespace SmartFiles.Core;
public sealed record StorageRow(string Category,long Bytes){public string Size=>$"{Bytes/1048576d:N1} MB";}
public static class StorageMaintenance {
 public const long LogLimit=50L*1024*1024,CacheLimit=100L*1024*1024,ModelCacheLimit=100L*1024*1024;
 static IEnumerable<FileInfo> Files(string folder){if(!Directory.Exists(folder))yield break;foreach(var path in Directory.EnumerateFiles(folder,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=true})){var f=new FileInfo(path);if((f.Attributes&FileAttributes.ReparsePoint)==0)yield return f;}}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,ExactSpelling=true)]static extern uint GetCompressedFileSizeW(string path,out uint high);
 static long FileSize(FileInfo file){if((file.Attributes&(FileAttributes.SparseFile|FileAttributes.Compressed))==0)return file.Length;var low=GetCompressedFileSizeW(file.FullName,out var high);if(low==uint.MaxValue&&Marshal.GetLastWin32Error()!=0)return file.Length;return checked((long)(((ulong)high<<32)|low));}
 static long Size(string folder)=>Files(folder).Sum(FileSize);
 public static List<StorageRow> Measure(string home){var rows=new List<StorageRow>{new("程序",Size(Path.Combine(home,"Release"))),new("模型",Size(Path.Combine(home,"Models"))),new("数据库与任务",Size(Path.Combine(home,"Data"))),new("日志",Size(Path.Combine(home,"Logs"))),new("缓存 / 模型缓存",Size(Path.Combine(home,"Cache"))+Size(Path.Combine(home,"ModelCache"))),new("临时文件",Size(Path.Combine(home,"Temp"))+Size(Path.Combine(home,"TempTransfer")))};var all=Size(home);rows.Add(new("开发工具 / 测试 / 其他",Math.Max(0,all-rows.Sum(x=>x.Bytes))));rows.Add(new("总计（不含用户分类目录）",all));return rows;}
 static bool CacheOwned(FileInfo file)=>file.Name is "node-embeddings.json" or "node-embeddings.json.new"||file.Name.StartsWith("sf-cache-",StringComparison.Ordinal);
 static void Trim(string folder,long limit,Func<FileInfo,bool> owned){var files=Files(folder).ToList();long total=files.Sum(x=>x.Length);foreach(var file in files.Where(owned).OrderBy(x=>x.LastWriteTimeUtc)){if(total<=limit)break;try{var size=file.Length;file.Delete();total-=size;}catch(IOException){}catch(UnauthorizedAccessException){}}}
 public static void Run(string home,HistoryService history,Settings settings,bool clearCache=false){
  FileIdentity.ValidatePath(home);Trim(Path.Combine(home,"Logs"),LogLimit,f=>f.Name.StartsWith("smartfiles.log")||f.Name.StartsWith("watcher.log"));Trim(Path.Combine(home,"Cache"),clearCache?0:CacheLimit,CacheOwned);Trim(Path.Combine(home,"ModelCache"),clearCache?0:ModelCacheLimit,f=>f.Name.StartsWith("sf-cache-"));
  var records=history.Read<MoveRecord>("transaction");var active=records.Where(x=>x.State is not ("Completed" or "Rollback" or "Undone")).Select(x=>x.Temp).ToHashSet(StringComparer.OrdinalIgnoreCase);
  // Only explicitly owned, completed-transaction files may be removed. Unknown Temp files are preserved.
  foreach(var tx in records.Where(x=>x.State is "Completed" or "Rollback" or "Undone")){if(tx.Temp.Length>0&&!active.Contains(tx.Temp)&&File.Exists(tx.Temp)&&FileIdentity.Under(tx.Temp,Path.Combine(home,"TempTransfer"))&&Path.GetFileName(tx.Temp)==tx.Id+".part"&&tx.Result!=null&&FileIdentity.Matches(tx.Temp,tx.Result))try{File.Delete(tx.Temp);}catch{}if(tx.Temp.Length>0){tx.Temp="";tx.TempIdentity="";history.Put("transaction",tx.Id,tx);}}
  foreach(var f in Files(Path.Combine(home,"Temp")).Where(x=>x.Name.StartsWith("sf-temp-")&&x.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-1)))if(!active.Contains(f.FullName))try{using(var held=new FileStream(f.FullName,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}f.Delete();}catch{}
  history.Prune(settings.HistoryMaxCount,settings.HistoryMaxDays);
 }
}
