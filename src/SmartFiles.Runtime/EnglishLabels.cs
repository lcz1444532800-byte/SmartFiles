namespace SmartFiles.Core;
// Read compatibility only: existing filenames, folders and settings are never renamed.
public static class EnglishLabels {
 static readonly Dictionary<string,string> Legacy=new(StringComparer.Ordinal) {
  ["\u5f85\u786e\u8ba4"]="Pending",
  ["\u786e\u8ba4\u540e\u79fb\u52a8"]="Move after confirmation",
  ["\u79fb\u52a8\u524d\u63d0\u9192"]="Warn before moving",
  ["\u53ea\u5206\u7c7b"]="Classify only",
  ["\u5ffd\u7565"]="Ignore",
  ["\u624b\u52a8\u6765\u6e90"]="Manual source",
  ["API\u56fe\u7247"]="API image",
  ["API\u6587\u672c"]="API text",
  ["\u672c\u5730\u89c6\u89c9 \u00b7 TinyCLIP"]="Local vision \u00b7 TinyCLIP",
  ["\u5386\u53f2\u5b66\u4e60"]="Learned rule",
  ["\u672c\u5730\u89c4\u5219"]="Local rules",
  ["\u6587\u4ef6\u540d / metadata"]="Filename / metadata",
  ["\u9700\u8981\u4eba\u5de5\u786e\u8ba4"]="Manual review needed",
  ["\u5f85\u5206\u6790"]="Not analyzed",
  ["\u5df2\u6574\u7406"]="Organized",
  ["\u5df2\u64a4\u9500"]="Undone",
  ["\u5df2\u56de\u6eda"]="Rolled back",
  ["\u51c6\u5907\u4e2d"]="Preparing",
  ["\u79fb\u52a8\u4e2d"]="Moving",
  ["\u6821\u9a8c\u4e2d"]="Verifying",
  ["\u6b63\u5728\u5b8c\u6210"]="Finishing",
  ["\u9700\u8981\u6062\u590d\u68c0\u67e5"]="Recovery review needed",
  ["\u5206\u6790\u4e2d"]="Analyzing",
  ["\u961f\u5217\u5df2\u6ee1\uff0c\u672a\u8fdb\u884c AI \u5206\u6790"]="Queue full; AI analysis was skipped",
  ["OCR \u4e0d\u53ef\u7528\uff0c\u5df2\u964d\u7ea7"]="OCR unavailable; using fallback",
  ["OCR / BGE\u5019\u9009"]="OCR / BGE candidates",
  ["\u672c\u5730\u8bed\u4e49\u6682\u4e0d\u53ef\u7528\uff0c\u4fdd\u7559\u89c4\u5219\u7ed3\u679c"]="Local semantics unavailable; keeping rule results",
  [" \u672c\u5730\u89c6\u89c9\u6682\u4e0d\u53ef\u7528"]=" Local vision is temporarily unavailable",
  [" \u6279\u6b21\u89c6\u89c9 API \u989d\u5ea6\u5df2\u7528\u5b8c\uff0c\u7b49\u5f85\u4eba\u5de5\u786e\u8ba4"]=" Batch vision API budget reached; manual review needed",
  ["\u5206\u6790\u5931\u8d25\uff0c\u53ef\u624b\u52a8\u9009\u62e9\u5206\u7c7b"]="Analysis failed; select a category manually",
  ["\u8f83\u786e\u5b9a"]="High",
  ["\u53ef\u80fd"]="Likely",
  ["\u4e0d\u786e\u5b9a"]="Uncertain",
  [" \u4fe1\u606f\u4e0d\u8db3\u6216 API \u672a\u542f\u7528\uff0c\u8bf7\u4eba\u5de5\u68c0\u67e5"]=" Not enough information or API disabled; review manually",
  ["\u6574\u7406\u4e2d"]="Organizing",
  ["\u4e3b\u52a8\u6295\u9012 \u00b7 _Inbox"]="Drop folder \u00b7 _Inbox",
  ["\u5fae\u4fe1"]="WeChat",
  ["QQ\u4fdd\u5b58\u76ee\u5f55\uff08\u8bf7\u6838\u5bf9\uff09"]="QQ downloads (verify path)",
  [" \u63a5\u6536\u6587\u4ef6"]=" received files",
  ["Worker \u8fde\u7eed\u4e09\u6b21\u5931\u8d25\uff0c\u5df2\u505c\u6b62\u81ea\u52a8\u91cd\u8bd5\uff0c\u8bf7\u4eba\u5de5\u9009\u62e9\u5206\u7c7b"]="Worker failed three times. Automatic retries stopped; select a category manually.",
  ["\u6b64\u4efb\u52a1\u5df2\u5931\u8d25\u4e09\u6b21\uff0c\u8bf7\u4eba\u5de5\u9009\u62e9\u5206\u7c7b"]="This task failed three times; select a category manually.",
  ["\u5f85\u786e\u8ba4\u961f\u5217\u5df2\u6ee1\uff0c\u8bf7\u5148\u5904\u7406\u5df2\u6709\u4efb\u52a1\u518d\u6062\u590d\u76d1\u542c"]="Pending queue full. Process existing tasks before resuming monitoring.",
 };
 public static string Text(string value){
  if(string.IsNullOrEmpty(value)||value.All(c=>c<128))return value;
  if(Legacy.TryGetValue(value,out var translated))return translated;
  foreach(var pair in Legacy.Where(x=>x.Key.Length>3).OrderByDescending(x=>x.Key.Length))value=value.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
  return value;
 }
 public static string SourceName(string value)=>string.Join(" / ",value.Split(" / ").Select(part=>part switch{ "\u5fae\u4fe1 \u63a5\u6536\u6587\u4ef6"=>"WeChat received files","QQ \u63a5\u6536\u6587\u4ef6"=>"QQ received files","\u4e3b\u52a8\u6295\u9012 \u00b7 _Inbox"=>"Drop folder · _Inbox","\u624b\u52a8\u6765\u6e90"=>"Manual source","QQ\u4fdd\u5b58\u76ee\u5f55\uff08\u8bf7\u6838\u5bf9\uff09"=>"QQ downloads (verify path)",_=>part}));
}
public static class BuiltinFolders {
 static readonly Dictionary<string,string> Legacy=new(StringComparer.OrdinalIgnoreCase) {
  ["\u5b66\u4e60"]="Study",
  ["\u5b66\u4e60\\\u8bfe\u7a0b\u8d44\u6599"]="Study\\Course Materials",
  ["\u5b66\u4e60\\\u4f5c\u4e1a"]="Study\\Assignments",
  ["\u5b66\u4e60\\\u8bba\u6587\u4e0e\u6587\u732e"]="Study\\Papers and References",
  ["\u5b66\u4e60\\\u7535\u5b50\u4e66"]="Study\\Ebooks",
  ["\u5b66\u4e60\\\u8003\u8bd5\u8d44\u6599"]="Study\\Exam Materials",
  ["AI\u4e0e\u5f00\u53d1"]="AI and Development",
  ["AI\u4e0e\u5f00\u53d1\\AI\u6a21\u578b"]="AI and Development\\AI Models",
  ["AI\u4e0e\u5f00\u53d1\\\u5de5\u4f5c\u6d41"]="AI and Development\\Workflows",
  ["AI\u4e0e\u5f00\u53d1\\\u4ee3\u7801\u4e0e\u9879\u76ee"]="AI and Development\\Code and Projects",
  ["AI\u4e0e\u5f00\u53d1\\\u6559\u7a0b\u4e0e\u6587\u6863"]="AI and Development\\Tutorials and Documentation",
  ["AI\u4e0e\u5f00\u53d1\\\u62a5\u9519\u4e0e\u8c03\u8bd5"]="AI and Development\\Errors and Debugging",
  ["\u8f6f\u4ef6"]="Software",
  ["\u8f6f\u4ef6\\\u5b89\u88c5\u5305"]="Software\\Installers",
  ["\u8f6f\u4ef6\\\u9a71\u52a8"]="Software\\Drivers",
  ["\u8f6f\u4ef6\\\u5de5\u5177"]="Software\\Tools",
  ["\u8f6f\u4ef6\\\u538b\u7f29\u5305"]="Software\\Archives",
  ["\u56fe\u7247\u4e0e\u7d20\u6750"]="Images and Assets",
  ["\u56fe\u7247\u4e0e\u7d20\u6750\\\u622a\u56fe"]="Images and Assets\\Screenshots",
  ["\u56fe\u7247\u4e0e\u7d20\u6750\\\u7167\u7247"]="Images and Assets\\Photos",
  ["\u56fe\u7247\u4e0e\u7d20\u6750\\\u804a\u5929\u56fe\u7247"]="Images and Assets\\Chat Images",
  ["\u56fe\u7247\u4e0e\u7d20\u6750\\\u8bbe\u8ba1\u7d20\u6750"]="Images and Assets\\Design Assets",
  ["\u5a92\u4f53"]="Media",
  ["\u5a92\u4f53\\\u89c6\u9891"]="Media\\Video",
  ["\u5a92\u4f53\\\u5f55\u5c4f"]="Media\\Screen Recordings",
  ["\u5a92\u4f53\\\u97f3\u9891"]="Media\\Audio",
  ["\u529e\u516c\u6587\u6863"]="Office Documents",
  ["\u529e\u516c\u6587\u6863\\\u8868\u683c"]="Office Documents\\Spreadsheets",
  ["\u529e\u516c\u6587\u6863\\\u6f14\u793a"]="Office Documents\\Presentations",
  ["\u529e\u516c\u6587\u6863\\\u8868\u5355\u4e0e\u8d44\u6599"]="Office Documents\\Forms and References",
  ["\u5176\u4ed6"]="Other",
  ["_\u5f85\u786e\u8ba4"]="_Review",
 };
 public static string Display(string value)=>Legacy.GetValueOrDefault(value,value);
 public static bool Matches(string value,string expected)=>string.Equals(Display(value),expected,StringComparison.OrdinalIgnoreCase);
 public static bool IsReview(string value)=>Matches(value,"_Review");
}
