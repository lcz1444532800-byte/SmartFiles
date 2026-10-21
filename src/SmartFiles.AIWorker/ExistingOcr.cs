using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
namespace SmartFiles.AIWorker;
// Reuses the installed PP-OCRv4 model files read-only. No DeepPick GUI/Python process or copied weights.
public sealed class ExistingOcr:IDisposable {
 readonly InferenceSession detector,recognizer;readonly string[] chars;
 public ExistingOcr(string folder){
  using var options=Program.Options();detector=new(Path.Combine(folder,"onnx","ch_PP-OCRv4_det_infer.onnx"),options);recognizer=new(Path.Combine(folder,"onnx","ch_PP-OCRv4_rec_infer.onnx"),options);
  chars=new[]{""}.Concat(File.ReadAllLines(Path.Combine(folder,"ppocr_keys_v1.txt"))).Append(" ").ToArray();
 }
 static BitmapSource Load(string path,int max){
  using var stream=File.OpenRead(path);var d=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);int w=d.Frames[0].PixelWidth,h=d.Frames[0].PixelHeight;stream.Position=0;
  var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;if(w>=h)b.DecodePixelWidth=Math.Min(max,w);else b.DecodePixelHeight=Math.Min(max,h);b.StreamSource=stream;b.EndInit();b.Freeze();return b;
 }
 static DenseTensor<float> Tensor(BitmapSource image,int width,int height,bool det){
  var resized=new TransformedBitmap(image,new ScaleTransform((double)width/image.PixelWidth,(double)height/image.PixelHeight));
  var bgr=new FormatConvertedBitmap(resized,PixelFormats.Bgr24,null,0);var data=new byte[width*height*3];bgr.CopyPixels(data,width*3,0);
  var tensor=new DenseTensor<float>(new[]{1,3,height,width});float[] mean=[.485f,.456f,.406f],std=[.229f,.224f,.225f];
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)for(int c=0;c<3;c++){var v=data[(y*width+x)*3+c]/255f;tensor[0,c,y,x]=det?(v-mean[c])/std[c]:(v-.5f)/.5f;}return tensor;
 }
 public string Read(string path){
  var image=Load(path,1280);int iw=image.PixelWidth,ih=image.PixelHeight;int w=Math.Max(32,(int)Math.Round(iw/32d)*32),h=Math.Max(32,(int)Math.Round(ih/32d)*32);
  using var detected=detector.Run(new[]{NamedOnnxValue.CreateFromTensor(detector.InputMetadata.Keys.First(),Tensor(image,w,h,true))});
  var map=detected.First().AsTensor<float>();int mh=map.Dimensions[^2],mw=map.Dimensions[^1];var data=map.ToArray();var visited=new bool[mw*mh];var boxes=new List<(int X,int Y,int R,int B)>();var queue=new Queue<int>();
  for(int at=0;at<visited.Length;at++){
   if(visited[at]||data[at]<.30)continue;int minX=at%mw,maxX=minX,minY=at/mw,maxY=minY,count=0;double score=0;visited[at]=true;queue.Enqueue(at);
   while(queue.TryDequeue(out int p)){
    int x=p%mw,y=p/mw;count++;score+=data[p];minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
    foreach(var next in new[]{x>0?p-1:-1,x+1<mw?p+1:-1,y>0?p-mw:-1,y+1<mh?p+mw:-1})if(next>=0&&!visited[next]&&data[next]>=.30){visited[next]=true;queue.Enqueue(next);}
   }
   if(count<8||score/count<.5||maxX-minX<3)continue;
   int pad=Math.Max(3,(int)Math.Ceiling((maxY-minY)*.75));
   boxes.Add((Math.Max(0,(minX-pad)*iw/mw),Math.Max(0,(minY-pad)*ih/mh),Math.Min(iw,(maxX+pad+1)*iw/mw),Math.Min(ih,(maxY+pad+1)*ih/mh)));
   if(boxes.Count>=160)break;
  }
  var sb=new StringBuilder();
  foreach(var box in boxes.OrderBy(x=>x.Y/12).ThenBy(x=>x.X)){
   int bw=box.R-box.X,bh=box.B-box.Y;if(bw<2||bh<2)continue;
   var crop=new CroppedBitmap(image,new System.Windows.Int32Rect(box.X,box.Y,bw,bh));
   int rw=Math.Clamp((int)Math.Ceiling(48d*bw/bh),16,1024);
   using var recognized=recognizer.Run(new[]{NamedOnnxValue.CreateFromTensor(recognizer.InputMetadata.Keys.First(),Tensor(crop,rw,48,false))});
   var scores=recognized.First().AsTensor<float>();int steps=scores.Dimensions[^2],classes=scores.Dimensions[^1];var values=scores.ToArray();int prev=-1;
   for(int t=0;t<steps;t++){int best=0;for(int c=1;c<classes;c++)if(values[t*classes+c]>values[t*classes+best])best=c;
    if(best>0&&best!=prev&&best<chars.Length&&values[t*classes+best]>.45)sb.Append(chars[best]);prev=best;
   }sb.Append(' ');if(sb.Length>=4000)break;
  }
  return sb.ToString()[..Math.Min(sb.Length,4000)];
 }
 public void Dispose(){detector.Dispose();recognizer.Dispose();}
}
