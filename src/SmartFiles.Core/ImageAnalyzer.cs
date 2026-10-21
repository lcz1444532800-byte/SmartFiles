using System.Text.Json;
namespace SmartFiles.Core;
public sealed class ImageAnalyzer(AIWorkerClient worker) {
 public async Task<(Evidence Evidence,Recommendation? Suggestion)> AnalyzeAsync(string path,Evidence evidence,IReadOnlyList<FolderNode> nodes,CancellationToken ct){
  var result=await worker.RequestAsync(new{op="vision",path},ct);
  var label=result.GetProperty("label").GetString()??"Other";
  double score=result.GetProperty("score").GetDouble(),margin=result.GetProperty("margin").GetDouble();
  var meta=evidence.Metadata+" 本地视觉粗分类："+label;
  string? relative=label switch {
   "Photo" or "Scenery" or "Person/photo"=>"图片与素材\\照片",
   "Screenshot" or "Software UI" or "Webpage"=>"图片与素材\\截图",
   "Chat screenshot"=>"图片与素材\\聊天图片",
   "Error/debug screen"=>"AI与开发\\报错与调试",
   "Code/editor"=>"AI与开发\\代码与项目",
   "Presentation"=>"办公文档\\演示",
   "Table"=>"办公文档\\表格",
   "Design asset" or "Diagram"=>"图片与素材\\设计素材",_=>null};
  var folder=nodes.FirstOrDefault(n=>n.Label==relative);
  // Zero-shot scores are not calibrated probabilities. Only strong separation creates a coarse local suggestion.
  Recommendation? suggestion=folder!=null&&score>=.65&&margin>=.35?new(folder.Id,.82,"本地视觉 · TinyCLIP"):null;
  return(evidence with{Metadata=meta},suggestion);
 }
}
