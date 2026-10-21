using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
namespace SmartFiles.AIWorker;
public sealed class BgeEmbedding:IDisposable {
 readonly InferenceSession session;readonly Dictionary<string,int> vocab;
 public BgeEmbedding(string folder){vocab=File.ReadAllLines(Path.Combine(folder,"vocab.txt")).Select((s,i)=>(s,i)).ToDictionary(x=>x.s,x=>x.i);using var options=Program.Options();session=new(Path.Combine(folder,"model.onnx"),options);}
 public long[] Tokenize(string input){
  var clean=new StringBuilder();
  foreach(var c in input.ToLowerInvariant().Normalize(NormalizationForm.FormD)){
   var cat=char.GetUnicodeCategory(c);if(cat==UnicodeCategory.NonSpacingMark||cat==UnicodeCategory.Control&&!char.IsWhiteSpace(c))continue;
   if(c>='\u4e00'&&c<='\u9fff'||char.IsPunctuation(c)||char.IsSymbol(c))clean.Append(' ').Append(c).Append(' ');else clean.Append(c);
  }
  var tokens=new List<long>{vocab["[CLS]"]};
  foreach(var word in Regex.Split(clean.ToString(),@"\s+").Where(x=>x.Length>0)){
   var pieces=new List<long>();int start=0;bool bad=word.Length>100;
   while(!bad&&start<word.Length){
    int end=word.Length,found=-1;for(;end>start;end--){var piece=(start==0?"":"##")+word[start..end];if(vocab.TryGetValue(piece,out found))break;}
    if(end==start){bad=true;break;}pieces.Add(found);start=end;
   }
   tokens.AddRange(bad?new long[]{vocab["[UNK]"]}:pieces);
   if(tokens.Count>=255)break;
  }
  if(tokens.Count>255)tokens=tokens.Take(255).ToList();tokens.Add(vocab["[SEP]"]);return tokens.ToArray();
 }
 public float[] Encode(string text){
  var ids=Tokenize(text);int n=ids.Length;var inputs=new List<NamedOnnxValue>();
  foreach(var name in session.InputMetadata.Keys){
   var data=name=="input_ids"?ids:name=="attention_mask"?Enumerable.Repeat(1L,n).ToArray():new long[n];
   inputs.Add(NamedOnnxValue.CreateFromTensor(name,new DenseTensor<long>(data,new[]{1,n})));
  }
  using var results=session.Run(inputs);var output=results.First().AsTensor<float>();int width=output.Dimensions[^1];
  var vector=output.ToArray().Take(width).ToArray();double norm=Math.Sqrt(vector.Sum(x=>(double)x*x));if(norm>0)for(int i=0;i<width;i++)vector[i]/=(float)norm;
  return vector;
 }
 public void Dispose()=>session.Dispose();
}
