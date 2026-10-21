using System.Text.Json;
namespace SmartFiles.Core;
public sealed class ImageAnalyzer(AIWorkerClient worker) {
 public async Task<(Evidence Evidence,Recommendation? Suggestion)> AnalyzeAsync(string path,Evidence evidence,IReadOnlyList<FolderNode> nodes,CancellationToken ct){
  var result=await worker.RequestAsync(new{op="vision",path},ct);
  var label=result.GetProperty("label").GetString()??"Other";
  double score=result.GetProperty("score").GetDouble(),margin=result.GetProperty("margin").GetDouble();
  var meta=evidence.Metadata+" Local vision category: "+label;
  string? relative=label switch {
   "Photo" or "Scenery" or "Person/photo"=>"Images and Assets\\Photos",
   "Screenshot" or "Software UI" or "Webpage"=>"Images and Assets\\Screenshots",
   "Chat screenshot"=>"Images and Assets\\Chat Images",
   "Error/debug screen"=>"AI and Development\\Errors and Debugging",
   "Code/editor"=>"AI and Development\\Code and Projects",
   "Presentation"=>"Office Documents\\Presentations",
   "Table"=>"Office Documents\\Spreadsheets",
   "Design asset" or "Diagram"=>"Images and Assets\\Design Assets",_=>null};
  var folder=nodes.FirstOrDefault(n=>BuiltinFolders.Matches(n.Label,relative??""));
  // Zero-shot scores are not calibrated probabilities. Only strong separation creates a coarse local suggestion.
  Recommendation? suggestion=folder!=null&&score>=.65&&margin>=.35?new(folder.Id,.82,"Local vision · TinyCLIP"):null;
  return(evidence with{Metadata=meta},suggestion);
 }
}
