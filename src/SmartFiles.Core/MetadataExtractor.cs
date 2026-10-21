using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SmartFiles.Core;
public record Evidence(string Name,string Extension,long Size,string Metadata,string Text,bool Image,bool OcrDerived=false,string SourceType="Download/received",double LocalConfidence=0);
public sealed class MetadataExtractor {
 public static bool IsImage(string path)=>new[]{".jpg",".jpeg",".png",".bmp",".webp",".tif",".tiff"}.Contains(Path.GetExtension(path).ToLowerInvariant());
 public Evidence Extract(string path) {
  string ext=Path.GetExtension(path).ToLowerInvariant(),meta="",text="";var size=new FileInfo(path).Length;
  try {
   if(new[]{".txt",".md",".csv",".json",".yaml",".yml",".log",".py",".cs",".js"}.Contains(ext)){
    using var sr=new StreamReader(path);var chars=new char[4096];text=new(chars,0,sr.ReadBlock(chars,0,chars.Length));
   }else if(ext is ".docx" or ".xlsx" or ".pptx" or ".zip"){
    // ZipArchive central-directory allocation is bounded by input size.
    if(size<=32*1024*1024){using var zip=ZipFile.OpenRead(path);
     if(ext==".zip")meta=string.Join(", ",zip.Entries.Take(60).Select(e=>e.FullName));
     else foreach(var name in new[]{"docProps/core.xml","word/document.xml","ppt/slides/slide1.xml","xl/sharedStrings.xml"}){
      var entry=zip.GetEntry(name);if(entry==null||entry.Length>1024*1024)continue;
      using var stream=entry.Open();using var reader=XmlReader.Create(stream,new(){DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=1024*1024,XmlResolver=null});
      var sb=new StringBuilder();while(reader.Read()&&sb.Length<4000)if(reader.NodeType==XmlNodeType.Text)sb.Append(reader.Value).Append(' ');
      if(name.StartsWith("docProps"))meta+=sb.ToString();else text+=sb.ToString();
     }
    }
   }else if(ext==".pdf"&&size<=24*1024*1024){
    using var pdf=UglyToad.PdfPig.PdfDocument.Open(path);meta=pdf.Information.Title??"";if(pdf.NumberOfPages>0)text=pdf.GetPage(1).Text;
   }else if(ext==".msi"){
    if(size<=128L*1024*1024)meta=MsiMetadata.Read(path);
    try{using var cert=System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);meta+=" Signing certificate (not verified online): "+cert.Subject;}catch{}
   }else if(ext==".exe"){
    var v=FileVersionInfo.GetVersionInfo(path);meta=$"{v.FileDescription} {v.ProductName} {v.CompanyName} {v.FileVersion}";try{using var cert=System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);meta+=" Signing certificate (not verified online): "+cert.Subject;}catch{}
   }else if(IsImage(path)){
    using var stream=File.OpenRead(path);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var frame=decoder.Frames[0];
    meta=$"{frame.PixelWidth}×{frame.PixelHeight}";
    if(frame.Metadata is BitmapMetadata m)meta+=$" Camera:{m.CameraManufacturer} {m.CameraModel} Title:{m.Title}";
   }
  }catch{}
  return new(Path.GetFileName(path),ext,size,meta[..Math.Min(2000,meta.Length)],text[..Math.Min(4000,text.Length)],IsImage(path));
 }
 public static byte[] Thumbnail(string path,int edge=768){
  using var stream=File.OpenRead(path);
  var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var wide=header.Frames[0].PixelWidth>=header.Frames[0].PixelHeight;stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;if(wide)image.DecodePixelWidth=edge;else image.DecodePixelHeight=edge;image.StreamSource=stream;image.EndInit();image.Freeze();
  BitmapSource source=image;
  if(image.PixelHeight>edge){var s=(double)edge/image.PixelHeight;source=new TransformedBitmap(image,new ScaleTransform(s,s));}
  var jpeg=new JpegBitmapEncoder{QualityLevel=75};jpeg.Frames.Add(BitmapFrame.Create(source));using var mem=new MemoryStream();jpeg.Save(mem);return mem.ToArray();
 }
}
