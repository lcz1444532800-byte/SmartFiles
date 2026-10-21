using System.Security.Cryptography;
namespace SmartFiles.Core;
public sealed class FileTransactionService(HistoryService history,string home,string root) {
 public bool StrictHash {get;set;}
 readonly SemaphoreSlim mutex=new(1,1);
 internal bool ForceCopyForTests {get;set;} internal Action<string>? FaultInjection {get;set;}
 public async Task WaitForIdleAsync(){await mutex.WaitAsync();mutex.Release();}
 public async Task<MoveRecord> MoveAsync(string source,string target,Stamp expected,string method,string undoOf="",CancellationToken ct=default) {
  await mutex.WaitAsync(ct); try {
   source=Path.GetFullPath(source);target=Path.GetFullPath(target);
   if(string.IsNullOrEmpty(undoOf)&&!FileIdentity.Under(target,root))throw new IOException("Destination is outside the category root");
   if(!string.IsNullOrEmpty(undoOf)){var original=history.Read<MoveRecord>("transaction").SingleOrDefault(x=>x.Id==undoOf&&x.State=="Completed");if(original==null||!string.Equals(source,original.Target,StringComparison.OrdinalIgnoreCase)||!string.Equals(target,original.Source,StringComparison.OrdinalIgnoreCase)||expected!=original.Result)throw new IOException("Undo request does not match a completed transaction");}
   FileIdentity.ValidatePath(source);FileIdentity.ValidatePath(target);FileIdentity.SafeName(Path.GetFileName(target));
   if(!Directory.Exists(Path.GetDirectoryName(target)))throw new IOException("Destination folder no longer exists. Refresh categories.");
   if(File.Exists(target)||Directory.Exists(target))throw new IOException("Filename conflict: keep both, choose another name, or cancel");
   using var pins=FileIdentity.PinParents(source,target);
   using var handle=FileIdentity.Open(source,true);
   if(FileIdentity.Read(handle)!=expected)throw new IOException("File content or identity changed. Review it again.");
   var tx=new MoveRecord{Source=source,Target=target,Original=expected,Method=method,UndoOf=undoOf};
   history.Put("transaction",tx.Id,tx);
   bool same=!ForceCopyForTests&&string.Equals(Path.GetPathRoot(source),Path.GetPathRoot(target),StringComparison.OrdinalIgnoreCase);
   try {
    tx.State="Moving";history.Put("transaction",tx.Id,tx);
    if(same) {
     FileIdentity.Rename(handle,target);
     tx.Result=FileIdentity.Read(handle);
    } else {
     var transfer=Path.Combine(home,"TempTransfer");Directory.CreateDirectory(transfer);FileIdentity.ValidatePath(transfer);
     tx.Temp=string.Equals(Path.GetPathRoot(target),Path.GetPathRoot(transfer),StringComparison.OrdinalIgnoreCase)?Path.Combine(transfer,tx.Id+".part"):Path.Combine(Path.GetDirectoryName(target)!,".smartfiles-"+tx.Id+".part");history.Put("transaction",tx.Id,tx);
     using var tempPins=FileIdentity.PinParents(tx.Temp);
     // A duplicate source handle is not needed: RandomAccess reads keep our delete/write lock intact.
     byte[]? fullHash=null;using var hasher=StrictHash?IncrementalHash.CreateHash(HashAlgorithmName.SHA256):null;
     await using(var output=new FileStream(tx.Temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,FileOptions.Asynchronous|FileOptions.WriteThrough)){
      tx.TempIdentity=FileIdentity.Read(output.SafeFileHandle).Id;history.Put("transaction",tx.Id,tx);
      var buffer=new byte[1024*1024];long offset=0;int n;
      while((n=RandomAccess.Read(handle,buffer,offset))>0){ct.ThrowIfCancellationRequested();hasher?.AppendData(buffer,0,n);await output.WriteAsync(buffer.AsMemory(0,n),ct);offset+=n;}output.Flush(true);fullHash=hasher?.GetHashAndReset();
     }
     using var tempHandle=FileIdentity.Open(tx.Temp,true);
     tx.Result=FileIdentity.Read(tempHandle);tx.State="Verifying";history.Put("transaction",tx.Id,tx);
     if(tx.Result.Length!=expected.Length||!SampleEqual(handle,tempHandle,expected.Length))throw new IOException("Copy verification failed");
     if(fullHash!=null&&!fullHash.AsSpan().SequenceEqual(HashHandle(tempHandle)))throw new IOException("Full SHA-256 verification failed");
     FileIdentity.Rename(tempHandle,target);
     tx.State="Published";history.Put("transaction",tx.Id,tx);
     try{FaultInjection?.Invoke("BeforeSourceDelete");FileIdentity.DeleteOpened(handle);}catch{FileIdentity.DeleteOpened(tempHandle);tx.State="Rollback";history.Put("transaction",tx.Id,tx);throw;}
    }
    // Handle disposal commits delete-on-close on a cross-volume source.
    handle.Dispose();
    if(File.Exists(source)||!FileIdentity.Matches(target,tx.Result!))throw new IOException("Move result needs recovery review");
    tx.State="Completed";tx.Temp="";tx.TempIdentity="";history.Put("transaction",tx.Id,tx);return tx;
   } catch {
    if(tx.State!="Rollback"&&CleanupPartial(tx)){tx.State="Rollback";}
    if(tx.State!="Rollback"){tx.State="NeedsRecovery";history.Put("transaction",tx.Id,tx);}
    history.Put("transaction",tx.Id,tx);throw;
   }
  } finally{mutex.Release();}
 }
 static byte[] HashHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle){using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[1024*1024];long offset=0;int n;while((n=RandomAccess.Read(handle,buffer,offset))>0){hash.AppendData(buffer,0,n);offset+=n;}return hash.GetHashAndReset();}
 static bool SampleEqual(Microsoft.Win32.SafeHandles.SafeFileHandle a,Microsoft.Win32.SafeHandles.SafeFileHandle b,long len){
  var x=new byte[65536];var y=new byte[65536];
  foreach(var offset in new[]{0L,Math.Max(0,len-65536)}){int n=RandomAccess.Read(a,x,offset);int m=RandomAccess.Read(b,y,offset);if(n!=m||!x.AsSpan(0,n).SequenceEqual(y.AsSpan(0,m)))return false;}return true;
 }
 public static string AvailableName(string folder,string name) {
  FileIdentity.SafeName(name);for(int i=1;i<10000;i++){var p=Path.Combine(folder,i==1?name:$"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");if(!File.Exists(p)&&!Directory.Exists(p))return p;}throw new IOException("Too many files with the same name");
 }
 bool CleanupPartial(MoveRecord tx){try{if(tx.Temp.Length==0||tx.TempIdentity.Length==0||!File.Exists(tx.Temp)||File.Exists(tx.Target)||!FileIdentity.Matches(tx.Source,tx.Original))return false;var regular=FileIdentity.Under(tx.Temp,Path.Combine(home,"TempTransfer"))&&Path.GetFileName(tx.Temp)==tx.Id+".part";var undo=tx.UndoOf.Length>0&&tx.Temp==Path.Combine(Path.GetDirectoryName(tx.Target)!,".smartfiles-"+tx.Id+".part");if(!regular&&!undo)return false;FileIdentity.ValidatePath(tx.Temp);using var pins=FileIdentity.PinParents(tx.Temp);using var h=FileIdentity.Open(tx.Temp,true);if(FileIdentity.Read(h).Id!=tx.TempIdentity)return false;FileIdentity.DeleteOpened(h);tx.Temp="";tx.TempIdentity="";return true;}catch{return false;}}
 public void Recover() {
  foreach(var tx in history.Read<MoveRecord>("transaction").Where(x=>x.State is not ("Completed" or "Rollback" or "Undone"))){
   try{
    if(CleanupPartial(tx)){tx.State="Rollback";history.Put("transaction",tx.Id,tx);continue;}
    var s=FileIdentity.Matches(tx.Source,tx.Original);
    var d=tx.Result!=null&&FileIdentity.Matches(tx.Target,tx.Result);
    if(!File.Exists(tx.Source)&&d)tx.State="Completed";
    else if(!File.Exists(tx.Source)&&FileIdentity.Matches(tx.Target,tx.Original)){tx.Result=tx.Original;tx.State="Completed";}
    else if(s&&d&&tx.Result!.Id!=tx.Original.Id){
     FileIdentity.ValidatePath(tx.Source);FileIdentity.ValidatePath(tx.Target);using var pins=FileIdentity.PinParents(tx.Source,tx.Target);using var src=FileIdentity.Open(tx.Source,true);if(FileIdentity.Read(src)!=tx.Original)throw new IOException("Source file identity changed");using var h=FileIdentity.Open(tx.Target,true);
     if(FileIdentity.Read(h)!=tx.Result)throw new IOException("Destination identity changed");
     FileIdentity.DeleteOpened(h);tx.State="Rollback";
    } else if(s&&!File.Exists(tx.Target)){
     if(tx.Temp.Length>0&&File.Exists(tx.Temp)){
      var regular=FileIdentity.Under(tx.Temp,Path.Combine(home,"TempTransfer"))&&Path.GetFileName(tx.Temp)==tx.Id+".part";var undoTemp=tx.UndoOf.Length>0&&string.Equals(tx.Temp,Path.Combine(Path.GetDirectoryName(tx.Target)!,".smartfiles-"+tx.Id+".part"),StringComparison.OrdinalIgnoreCase);if(!regular&&!undoTemp)throw new IOException("Invalid temporary path");
      // Unverified partial files are retained for manual recovery; never delete by path alone.
      if(tx.Result!=null&&FileIdentity.Matches(tx.Temp,tx.Result)){using var h=FileIdentity.Open(tx.Temp,true);if(FileIdentity.Read(h)==tx.Result)FileIdentity.DeleteOpened(h);tx.State="Rollback";}
      else tx.State="NeedsReview";
     }else tx.State="Rollback";
    }else tx.State="NeedsReview";
   }catch{tx.State="NeedsReview";}
   history.Put("transaction",tx.Id,tx);
  }
 }
}
public sealed class UndoService(FileTransactionService transactions,HistoryService history) {
 public async Task UndoAsync(MoveRecord record) {
  if(record.State!="Completed"||record.Result==null)throw new IOException("This record cannot be undone");
  if(!FileIdentity.Matches(record.Target,record.Result))throw new IOException("The file was modified, renamed or moved. Refusing to overwrite it.");
  if(File.Exists(record.Source)||Directory.Exists(record.Source))throw new IOException("The original path is occupied. Resolve the conflict first.");
  await transactions.MoveAsync(record.Target,record.Source,record.Result,"Undo",record.Id);
  record.State="Undone";history.Put("transaction",record.Id,record);
 }
}
