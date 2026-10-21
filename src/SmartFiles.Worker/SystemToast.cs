using Microsoft.Win32;
using SmartFiles.Core;
using System.Security;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
namespace SmartFiles.Worker;
public static class SystemToast {
 public static void Show(FileTask task,string home){try{
  // Late results are persisted silently, never open or activate the UI.
  var settings=RuntimeHost.ReadSettings(home);if((DateTime.UtcNow-task.SeenUtc).TotalMilliseconds>settings.InteractiveTtlMs)return;
  using(var id=Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\SmartFiles")){id.SetValue("DisplayName","SmartFiles");id.SetValue("IconUri",Path.Combine(RuntimeHost.DefaultHome,"Release","SmartFiles.exe"));}
  using(var protocol=Registry.CurrentUser.CreateSubKey(@"Software\Classes\smartfiles")){protocol.SetValue("","URL:SmartFiles");protocol.SetValue("URL Protocol","");using var command=protocol.CreateSubKey(@"shell\open\command");command.SetValue("",$"\"{RuntimeHost.DefaultHome}\\Release\\SmartFiles.exe\" --open-task \"%1\"");}
  var xml=new XmlDocument();xml.LoadXml($"<toast activationType='protocol' launch='smartfiles://task/{task.Id}'><visual><binding template='ToastGeneric'><text>发现新文件：{SecurityElement.Escape(task.Name)}</text><text>建议：{SecurityElement.Escape(task.TargetLabel)}</text></binding></visual></toast>");
  var toast=new ToastNotification(xml){Tag=task.Id[..16],Group="SmartFiles",ExpirationTime=DateTimeOffset.Now.AddMinutes(5)};ToastNotificationManager.CreateToastNotifier("SmartFiles").Show(toast);
  File.WriteAllText(Path.Combine(home,"Data","last-notification.json"),System.Text.Json.JsonSerializer.Serialize(new{taskId=task.Id,submittedUtc=DateTime.UtcNow}));
 }catch(Exception ex){new SafeLog(home).Write("toast_unavailable",ex);}}
}
