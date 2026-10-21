using System.Text.RegularExpressions;
namespace SmartFiles.Core;
public static partial class PrivacyBoundary {
 [GeneratedRegex(@"(?i)(?:[a-z]:[\\/]|\\\\)[^\r\n\t\""<>|]+")]
 private static partial Regex AbsolutePath();
 public static string Text(string value,int limit)=>AbsolutePath().Replace(value??"","[local path]")[..Math.Min(limit,AbsolutePath().Replace(value??"","[local path]").Length)];
 public static bool NeedsThumbnail(Evidence evidence)=>evidence.Image&&evidence.Text.Trim().Length<24;
}
