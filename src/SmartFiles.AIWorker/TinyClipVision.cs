using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
namespace SmartFiles.AIWorker;
public record VisualResult(string Label,double Score,double Margin,Dictionary<string,double> Scores);
public interface ILocalVisionProvider:IDisposable { VisualResult Analyze(string path); }
public sealed class TinyClipVision:ILocalVisionProvider {
 readonly InferenceSession session;readonly Dictionary<string,int> vocabulary,merges;
 static readonly (string Label,string Prompt)[] Categories=[
 ("Screenshot","a screenshot of a computer screen"),("Photo","a photograph"),
 ("Document","a scanned document with printed text"),("Chat screenshot","a screenshot of chat messages"),
 ("Code/editor","a screenshot of source code in a code editor"),("Software UI","a screenshot of a software application"),
 ("Error/debug screen","an error message on a computer screen"),("Diagram","a diagram or chart"),
 ("Presentation","a presentation slide"),("Table","a table or spreadsheet"),
 ("Webpage","a screenshot of a webpage"),("Meme","an internet meme"),
 ("Design asset","a graphic design illustration"),("Scenery","a photograph of a landscape"),
 ("Person/photo","a photograph of a person"),("Other","an abstract image")];
 readonly long[] tokenData,maskData;readonly int sequence;
 public TinyClipVision(string folder){
  using var options=Program.Options();session=new(Path.Combine(folder,"model.onnx"),options);
  vocabulary=JsonSerializer.Deserialize<Dictionary<string,int>>(File.ReadAllText(Path.Combine(folder,"vocab.json")))!;
  merges=File.ReadAllLines(Path.Combine(folder,"merges.txt")).Where(x=>!x.StartsWith("#")&&x.Contains(' ')).Select((s,i)=>(s,i)).ToDictionary(x=>x.s,x=>x.i);
  var rows=Categories.Select(c=>Tokens(c.Prompt)).ToArray();sequence=rows.Max(x=>x.Length);
  tokenData=new long[rows.Length*sequence];maskData=new long[tokenData.Length];
  for(int row=0;row<rows.Length;row++)for(int i=0;i<sequence;i++){tokenData[row*sequence+i]=i<rows[row].Length?rows[row][i]:vocabulary["<|endoftext|>"];maskData[row*sequence+i]=i<rows[row].Length?1:0;}
 }
 long[] Tokens(string text){
  var ids=new List<long>{vocabulary["<|startoftext|>"]};
  foreach(Match match in Regex.Matches(text.ToLowerInvariant(),@"[a-z]+|[0-9]|[^\s]")){
   var word=match.Value;
   if(vocabulary.TryGetValue(word+"</w>",out int direct)){ids.Add(direct);continue;}
   var parts=word.Select(c=>c.ToString()).ToList();parts[^1]+="</w>";
   while(parts.Count>1){
    int at=-1,rank=int.MaxValue;for(int i=0;i<parts.Count-1;i++)if(merges.TryGetValue(parts[i]+" "+parts[i+1],out int r)&&r<rank){rank=r;at=i;}
    if(at<0)break;parts[at]+=parts[at+1];parts.RemoveAt(at+1);
   }
   ids.AddRange(parts.Select(p=>(long)vocabulary.GetValueOrDefault(p,vocabulary["<|endoftext|>"])));
  }ids.Add(vocabulary["<|endoftext|>"]);return ids.ToArray();
 }
 static DenseTensor<float> Pixels(string path){
  using var stream=File.OpenRead(path);var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
  bool wide=header.Frames[0].PixelWidth>=header.Frames[0].PixelHeight;stream.Position=0;
  var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;if(wide)bitmap.DecodePixelWidth=1024;else bitmap.DecodePixelHeight=1024;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
  int square=Math.Min(bitmap.PixelWidth,bitmap.PixelHeight);
  var crop=new CroppedBitmap(bitmap,new System.Windows.Int32Rect((bitmap.PixelWidth-square)/2,(bitmap.PixelHeight-square)/2,square,square));
  var resized=new TransformedBitmap(crop,new ScaleTransform(224d/square,224d/square));var rgb=new FormatConvertedBitmap(resized,PixelFormats.Rgb24,null,0);
  var data=new byte[224*224*3];rgb.CopyPixels(data,224*3,0);float[] mean=[.48145466f,.4578275f,.40821073f],std=[.26862954f,.26130258f,.27577711f];var tensor=new DenseTensor<float>(new[]{1,3,224,224});
  for(int y=0;y<224;y++)for(int x=0;x<224;x++)for(int c=0;c<3;c++)tensor[0,c,y,x]=(data[(y*224+x)*3+c]/255f-mean[c])/std[c];
  return tensor;
 }
 public VisualResult Analyze(string path){
  var input=new List<NamedOnnxValue>();
  foreach(var name in session.InputMetadata.Keys){
   if(name=="pixel_values")input.Add(NamedOnnxValue.CreateFromTensor(name,Pixels(path)));
   else input.Add(NamedOnnxValue.CreateFromTensor(name,new DenseTensor<long>(name=="input_ids"?tokenData:maskData,new[]{Categories.Length,sequence})));
  }
  using var outputs=session.Run(input);var logits=outputs.First(x=>x.Name=="logits_per_image").AsTensor<float>().ToArray();
  var max=logits.Max();var exp=logits.Select(x=>Math.Exp(x-max)).ToArray();var sum=exp.Sum();
  var scores=Categories.Select((c,i)=>(c.Label,Score:exp[i]/sum)).OrderByDescending(x=>x.Score).ToArray();
  return new(scores[0].Label,scores[0].Score,scores[0].Score-scores[1].Score,scores.ToDictionary(x=>x.Label,x=>x.Score));
 }
 public void Dispose()=>session.Dispose();
}
