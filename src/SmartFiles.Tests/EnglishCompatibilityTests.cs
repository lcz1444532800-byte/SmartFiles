using System.IO;
using SmartFiles.Core;
namespace SmartFiles.Tests;
static class EnglishCompatibilityTests {
 public static void Run(string root){
  void Assert(bool value,string message){if(!value)throw new Exception(message);}
  var legacy=new FileTask{Status="\u5f85\u786e\u8ba4",Method="\u672c\u5730\u89c4\u5219",ConfidenceBand="\u8f83\u786e\u5b9a",Note="OCR \u4e0d\u53ef\u7528\uff0c\u5df2\u964d\u7ea7",SourceName="\u5fae\u4fe1 \u63a5\u6536\u6587\u4ef6"};
  var loaded=Json.Decode<FileTask>(Json.Encode(legacy));
  Assert(loaded.Status=="Pending"&&loaded.Method=="Local rules"&&loaded.ConfidenceBand=="High"&&loaded.Note=="OCR unavailable; using fallback","legacy task labels were not normalized");
  Assert(loaded.SourceDisplayName=="WeChat received files","legacy source display is not English");
  var label="\u8f6f\u4ef6\\\u5b89\u88c5\u5305";
  var node=new FolderNode("existing-folder",Path.Combine(root,"existing"),label);
  using var history=new HistoryService(Path.Combine(root,"english-compatibility"));
  var classifier=new LocalClassifier(new RuleEngine(history));
  Assert(node.DisplayLabel=="Software\\Installers"&&node.Label==label,"existing folder identity or label was changed");
  Assert(classifier.Classify(new("setup.exe",".exe",100,"","",false),"Downloads",new[]{node}).FolderId==node.Id,"legacy folder no longer receives installer suggestions");
  Assert(!new CandidateSelector().Select("document",new[]{new FolderNode("review","x","_\u5f85\u786e\u8ba4"),node},5).Any(x=>x.Id=="review"),"legacy review folder was offered to AI");
  var custom="Custom \u6587\u4ef6";Assert(BuiltinFolders.Display(custom)==custom,"custom folder label was translated");
  Assert(new MoveRecord{State="Completed"}.StateLabel=="Organized","history label is not English");
  using var taxonomy=new TaxonomyService(Path.Combine(root,"english-tree"),history);
  Assert(taxonomy.Nodes.Any(n=>n.Label=="Software\\Installers")&&taxonomy.Nodes.Any(n=>n.Label=="_Review"),"new default categories are not English");
 }
}
