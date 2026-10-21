using System.IO;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Win32.SafeHandles;
using SmartFiles.Core;
namespace SmartFiles.Tests;
static class StoragePrivacyTests {
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle h,uint code,IntPtr input,uint insize,IntPtr output,uint outsize,out uint returned,IntPtr overlapped);
 [DllImport("msi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern uint MsiOpenDatabaseW(string path,nint mode,out uint db);
 [DllImport("msi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern uint MsiDatabaseOpenViewW(uint db,string query,out uint view);
 [DllImport("msi.dll",ExactSpelling=true)]static extern uint MsiViewExecute(uint view,uint record);
 [DllImport("msi.dll",ExactSpelling=true)]static extern uint MsiCloseHandle(uint handle);
 [DllImport("msi.dll",ExactSpelling=true)]static extern uint MsiDatabaseCommit(uint db);
 static void MsiFixture(string path){Assert(MsiOpenDatabaseW(path,3,out var db)==0,"MSI create fixture failed");try{
  foreach(var sql in new[]{"CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)","INSERT INTO `Property` (`Property`,`Value`) VALUES ('ProductName','合成测试软件')","INSERT INTO `Property` (`Property`,`Value`) VALUES ('Manufacturer','Synthetic Publisher')","INSERT INTO `Property` (`Property`,`Value`) VALUES ('ProductVersion','1.2.3')"}){
   Assert(MsiDatabaseOpenViewW(db,sql,out var view)==0,"MSI fixture query failed");try{Assert(MsiViewExecute(view,0)==0,"MSI fixture execute failed");}finally{MsiCloseHandle(view);}
  }Assert(MsiDatabaseCommit(db)==0,"MSI fixture commit failed");}finally{MsiCloseHandle(db);}}
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 public static Task Run(string root){Directory.CreateDirectory(root);using var db=new HistoryService(root);var settings=new Settings();
  using(var connection=new SqliteConnection($"Data Source={Path.Combine(root,"Data","smartfiles.db")}")){connection.Open();using var tx=connection.BeginTransaction();using var c=connection.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO records(kind,id,json) VALUES('transaction',$id,$json)";var id=c.Parameters.Add("$id",SqliteType.Text);var json=c.Parameters.Add("$json",SqliteType.Text);for(int i=0;i<100000;i++){var r=new MoveRecord{Id=i.ToString(),Source="synthetic source",Target="synthetic target",State="Completed",Time=DateTime.UtcNow.AddMinutes(-i)};id.Value=r.Id;json.Value=Json.Encode(r);c.ExecuteNonQuery();}tx.Commit();}
  db.Put("transaction","active",new MoveRecord{Id="active",State="NeedsRecovery",Time=DateTime.UtcNow.AddYears(-2)});db.Put("transaction","expired",new MoveRecord{Id="expired",State="Completed",Time=DateTime.UtcNow.AddDays(-181)});
  foreach(var category in new[]{"Logs","Cache","ModelCache","Temp"})Directory.CreateDirectory(Path.Combine(root,category));
  void Large(string name,int mb){using var f=File.Create(name);var block=new byte[1024*1024];for(int i=0;i<mb;i++)f.Write(block);}
  for(int i=0;i<12;i++){Large(Path.Combine(root,"Logs","smartfiles.log."+i),5);Large(Path.Combine(root,"Cache","sf-cache-"+i),10);Large(Path.Combine(root,"ModelCache","sf-cache-"+i),10);}
  var unknown=Path.Combine(root,"Temp","unowned.txt");File.WriteAllText(unknown,"preserve unknown files");StorageMaintenance.Run(root,db,settings);
  long Size(string folder)=>Directory.EnumerateFiles(Path.Combine(root,folder)).Sum(f=>new FileInfo(f).Length);
  Assert(db.Count("transaction")==10001,"history not bounded or active transaction pruned");Assert(db.Find<MoveRecord>("transaction","expired")==null,"expired history retained");Assert(db.Find<MoveRecord>("transaction","active")!=null,"active transaction lost");Assert(Size("Logs")<=StorageMaintenance.LogLimit&&Size("Cache")<=StorageMaintenance.CacheLimit&&Size("ModelCache")<=StorageMaintenance.ModelCacheLimit,"storage cap failed");Assert(File.Exists(unknown),"unknown temp deleted");
  var report=new{simulatedRecords=100000,retainedIncludingActive=db.Count("transaction"),logsBytes=Size("Logs"),cacheBytes=Size("Cache"),modelCacheBytes=Size("ModelCache"),databaseBytes=new FileInfo(Path.Combine(root,"Data","smartfiles.db")).Length};File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\storage-stress.json",Json.Encode(report));
  var model=Path.Combine(root,"10gb.gguf");using(var f=File.Create(model)){Assert(DeviceIoControl(f.SafeFileHandle,0x900C4,IntPtr.Zero,0,IntPtr.Zero,0,out _,IntPtr.Zero),"sparse fixture failed");f.SetLength(10L*1024*1024*1024);f.Write(Encoding.UTF8.GetBytes("DO_NOT_READ_MODEL_WEIGHTS"));}
  var watch=Stopwatch.StartNew();var e=new MetadataExtractor().Extract(model);Assert(e.Text==""&&e.Metadata==""&&e.Size==10L*1024*1024*1024&&watch.ElapsedMilliseconds<1000,"model file read as content");
  var zip=Path.Combine(root,"archive.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("folder/document.txt").Open());w.Write("ARCHIVE_CONTENT_MUST_NOT_BE_SENT");}var ze=new MetadataExtractor().Extract(zip);Assert(ze.Text==""&&!ze.Metadata.Contains("MUST_NOT")&&ze.Metadata.Contains("document.txt"),"archive content extracted");
  var pdfPath=Path.Combine(root,"limited.pdf");var streamText="BT /F1 10 Tf 10 700 Td ("+new string('A',8000)+"END_OF_FULL_DOCUMENT_SECRET) Tj ET";var objects=new[]{"<< /Type /Catalog /Pages 2 0 R >>","<< /Type /Pages /Kids [3 0 R] /Count 1 >>","<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>","<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>","<< /Length "+Encoding.ASCII.GetByteCount(streamText)+" >>\nstream\n"+streamText+"\nendstream"};var document=new StringBuilder("%PDF-1.4\n");var offsets=new List<int>{0};for(int i=0;i<objects.Length;i++){offsets.Add(document.Length);document.Append(i+1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");}var xref=document.Length;document.Append("xref\n0 6\n0000000000 65535 f \n");foreach(var offset in offsets.Skip(1))document.Append(offset.ToString("D10")).Append(" 00000 n \n");document.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");File.WriteAllText(pdfPath,document.ToString(),Encoding.ASCII);var pe=new MetadataExtractor().Extract(pdfPath);Assert(pe.Text.Length>0&&pe.Text.Length<=4000&&!pe.Text.Contains("END_OF_FULL_DOCUMENT"),"PDF content not limited");Assert(PrivacyBoundary.Text(pe.Text,2000).Length<=2000,"API excerpt not bounded");
  var imagePath=Path.Combine(root,"large-image.png");var pixels=new byte[4096*3072*3];for(int i=0;i<pixels.Length;i++)pixels[i]=(byte)(i%251);var image=BitmapSource.Create(4096,3072,96,96,PixelFormats.Rgb24,null,pixels,4096*3);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(imagePath))png.Save(file);var thumb=MetadataExtractor.Thumbnail(imagePath);using(var memory=new MemoryStream(thumb)){var decoder=BitmapDecoder.Create(memory,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Assert(decoder.Frames[0].PixelWidth<=768&&decoder.Frames[0].PixelHeight<=768&&thumb.Length<=524288,"original/oversized image sent");}Assert(!thumb.AsSpan().SequenceEqual(File.ReadAllBytes(imagePath)),"original image copied as thumbnail");
  Assert(!PrivacyBoundary.NeedsThumbnail(new("scan.png",".png",100,"",new string('文',40),true,true)),"OCR sufficient still attaches image");Assert(!PrivacyBoundary.Text(@"title D:\Private\sensitive.pdf",2000).Contains(@"D:\"),"absolute path leaked");
  var msi=Path.Combine(root,"metadata-only.msi");MsiFixture(msi);var before=File.ReadAllBytes(msi);var me=new MetadataExtractor().Extract(msi);Assert(me.Metadata.Contains("合成测试软件")&&me.Metadata.Contains("Synthetic Publisher")&&me.Metadata.Contains("1.2.3")&&me.Text=="","MSI properties missing or package content extracted");Assert(before.AsSpan().SequenceEqual(File.ReadAllBytes(msi)),"MSI modified while reading metadata");
  return Task.CompletedTask;
 }
}
