using System.Text.RegularExpressions;
namespace SmartFiles.Core;
public record Recommendation(string FolderId,double Confidence,string Method,string Branch="",string Name="");
public sealed class RuleEngine(HistoryService history) {
 public List<LearnedRule> Rules=>history.Read<LearnedRule>("rule");
 public void Save(LearnedRule rule){if(string.IsNullOrWhiteSpace(rule.Keyword)||rule.Keyword.Length>80)throw new InvalidDataException("关键词长度无效");history.Put("rule",rule.Id,rule);}
 public void Delete(string id)=>history.Delete("rule",id);
 public void Learn(string features,string folderId){
  var snapshot=Rules;foreach(var word in Tokens(features).Where(x=>x.Length>=2).Take(12)){
   var r=snapshot.FirstOrDefault(x=>x.Keyword==word&&x.FolderId==folderId);if(r==null){r=new(){Keyword=word,FolderId=folderId};snapshot.Add(r);}
   r.Hits++;r.LastUsed=DateTime.UtcNow;Save(r);
  }
 }
 public static IEnumerable<string> Tokens(string input){
  var matches=Regex.Matches(input.ToLowerInvariant(),@"[a-z][a-z0-9+.-]{1,30}|[\p{IsCJKUnifiedIdeographs}]{2,16}");
  foreach(Match m in matches){yield return m.Value;if(m.Value.Any(c=>c>255)&&m.Value.Length>3)for(int i=0;i<m.Value.Length-1;i++)yield return m.Value.Substring(i,2);}
 }
 public Recommendation? Match(string text,IReadOnlyList<FolderNode> nodes){
  var ids=nodes.Select(x=>x.Id).ToHashSet();
  var best=Rules.Where(r=>r.Enabled&&r.Hits>=3&&ids.Contains(r.FolderId)&&text.Contains(r.Keyword,StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.Confidence).FirstOrDefault();
  return best==null?null:new(best.FolderId,best.Confidence,"历史学习");
 }
}
public sealed class CandidateSelector {
 public List<FolderNode> Select(string text,IReadOnlyList<FolderNode> nodes,int count,Dictionary<string,double>? semantic=null) {
  var words=RuleEngine.Tokens(text).Distinct().ToArray();
  return nodes.Where(n=>n.Label!="_待确认").Select(n=>new {Node=n,Score=words.Sum(w=>(n.Label+" "+n.Description+" "+n.Keywords).Contains(w,StringComparison.OrdinalIgnoreCase)?(w.Length>2?3:1):0)+(semantic?.GetValueOrDefault(n.Id)??0)*5})
   .OrderByDescending(n=>n.Score).ThenByDescending(n=>n.Node.Label.Count(c=>c=='\\')).Take(Math.Clamp(count,5,15)).Select(n=>n.Node).ToList();
 }
}
public sealed class LocalClassifier(RuleEngine rules) {
 public Recommendation Classify(Evidence e,string source,IReadOnlyList<FolderNode> nodes) {
  var text=e.Name+" "+e.Metadata+" "+e.Text;
  var learned=rules.Match(text,nodes);if(learned!=null)return learned;
  string? label=e.Extension switch{
   ".exe" or ".msi"=>"软件\\安装包", ".safetensors" or ".gguf" or ".onnx"=>"AI与开发\\AI模型",
   ".mp4" or ".mkv" or ".mov" or ".avi"=>"媒体\\视频",
   ".mp3" or ".wav" or ".flac"=>"媒体\\音频",
   ".xlsx" or ".xls" or ".csv"=>"办公文档\\表格",
   ".pptx" or ".ppt"=>"办公文档\\演示",
   ".zip" or ".7z" or ".rar"=>"软件\\压缩包",_=>null};
  var name=nodes.FirstOrDefault(n=>n.Label==label);
  if(name!=null)return new(name.Id,e.Extension is ".zip" or ".7z" or ".rar"?.83:.98,"本地规则");
  var matches=nodes.Where(n=>!n.Label.StartsWith("_")).Select(n=>new{N=n,Score=n.Label.Split('\\').Count(p=>p.Length>1&&text.Contains(p,StringComparison.OrdinalIgnoreCase))}).Where(x=>x.Score>0).OrderByDescending(x=>x.Score).ThenByDescending(x=>x.N.Label.Length).ToList();
  if(matches.Count>0){var m=matches[0];return new(m.N.Id,m.Score>=3?.96:m.Score==2?.88:.65,"文件名 / metadata");}
  if(e.Image&&Regex.IsMatch(e.Name,"screenshot|屏幕截图|截屏",RegexOptions.IgnoreCase)){
   var node=nodes.FirstOrDefault(n=>n.Label=="图片与素材\\截图");if(node!=null)return new(node.Id,.82,"本地规则");
  }
  return new(nodes.FirstOrDefault(n=>n.Label=="_待确认")?.Id??"",0,"需要人工确认");
 }
}
