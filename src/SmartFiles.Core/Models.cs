using System.Text.Json;
namespace SmartFiles.Core;
public enum SourcePolicy { NormalMove, AskBeforeMove, ClassifyOnly, Ignore }
public sealed class Source {
 public string Name {get;set;}=""; public string Path {get;set;}=""; public bool Enabled {get;set;}
 public SourcePolicy Policy {get;set;}=SourcePolicy.NormalMove;
 public bool Recursive {get;set;} public bool WarningAcknowledged {get;set;}
}
public sealed class Settings {
 public bool CloseToTray {get;set;}
 public int WorkerIdleSeconds {get;set;}=45;
 public string BackgroundMode {get;set;}="Realtime";
 public int HistoryMaxCount {get;set;}=10000; public int HistoryMaxDays {get;set;}=180;
 public bool SendOcr {get;set;}=true;
 public int AiDefaultsVersion {get;set;}
 public string Root {get;set;}=@"D:\SmartFiles"; public bool Monitoring {get;set;}
 public List<Source> Sources {get;set;}=[];
 public bool Notifications {get;set;}=true; public bool RenameSuggestions {get;set;}
 public bool Classification {get;set;}=true; public bool AutoStart {get;set;}
 public double LocalThreshold {get;set;}=.80; public double HighThreshold {get;set;}=.95;
 public int InteractiveTtlMs {get;set;}=4000; public int BatchWindowMs {get;set;}=2200;
 public int CandidateCount {get;set;}=10; public int ApiConcurrency {get;set;}=2;
 public int ApiTimeoutSeconds {get;set;}=20; public int ImageApiBudgetPerBatch {get;set;}=12;
 public string BaseUrl {get;set;}="https://api.deepseek.com"; public string TextModel {get;set;}="deepseek-flash"; public string VisionModel {get;set;}="deepseek-flash";
 public bool SendFilename {get;set;}=true; public bool SendMetadata {get;set;}=true;
 public bool SendText {get;set;}=true; public bool SendThumbnail {get;set;}=true;
 public bool LocalAi {get;set;}=true; public bool Ocr {get;set;}=true;
 public bool StrictHash {get;set;}
}
public record Stamp(string Id,long Length,long WriteTicks);
public record FolderNode(string Id,string Path,string Label,string Description="",string Keywords="");
public sealed class FileTask {
 public string Id {get;set;}=Guid.NewGuid().ToString("N"); public string Path {get;set;}="";
 public string SourcePath {get;set;}=""; public string SourceName {get;set;}="";
 public Stamp Stamp {get;set;}=new("",0,0); public DateTime SeenUtc {get;set;}=DateTime.UtcNow;
 public string FolderId {get;set;}=""; public string TargetLabel {get;set;}="_待确认";
 public string Method {get;set;}="待分析"; public double Confidence {get;set;}
 public string Status {get;set;}="待确认"; public string SuggestedName {get;set;}="";
 public string BranchName {get;set;}=""; public string Note {get;set;}="";
 public string Features {get;set;}=""; public string BatchId {get;set;}="";
 public string ConfidenceBand {get;set;}=""; public string ConfidenceDisplay=>$"{Confidence:P0} {ConfidenceBand}";
 public string Name=>System.IO.Path.GetFileName(Path);
}
public sealed class MoveRecord {
 public string Id {get;set;}=Guid.NewGuid().ToString("N"); public string Source {get;set;}="";
 public string Target {get;set;}=""; public string Temp {get;set;}=""; public string TempIdentity {get;set;}="";
 public Stamp Original {get;set;}=new("",0,0); public Stamp? Result {get;set;}
 public string State {get;set;}="Preparing"; public DateTime Time {get;set;}=DateTime.UtcNow;
 public string StateLabel=>State switch{"Completed"=>"已整理","Undone"=>"已撤销","Rollback"=>"已回滚","Preparing"=>"准备中","Moving"=>"移动中","Verifying"=>"校验中","Published"=>"正在完成",_=>"需要恢复检查"};
 public string Method {get;set;}=""; public string UndoOf {get;set;}=""; public string Error {get;set;}="";
}
public sealed class LearnedRule {
 public string Id {get;set;}=Guid.NewGuid().ToString("N"); public string Keyword {get;set;}="";
 public string FolderId {get;set;}=""; public int Hits {get;set;} public bool Enabled {get;set;}=true;
 public DateTime LastUsed {get;set;}=DateTime.UtcNow; public double Confidence=>Math.Min(.97,.55+Hits*.08);
}
