using System;
using System.Linq;
using System.Windows;
using SmartFiles.Core;
using Forms=System.Windows.Forms;
namespace SmartFiles;
public sealed class NotificationService:IDisposable {
 readonly Forms.NotifyIcon icon;readonly SmartFilesEngine engine;readonly System.Threading.Timer timer;readonly object gate=new();DateTime oldest;int count;
 public NotificationService(SmartFilesEngine e,Action open,Action exit){
  engine=e;icon=new(){Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??System.Drawing.SystemIcons.Application,Visible=true,Text="SmartFiles · Monitoring off"};
  var menu=new Forms.ContextMenuStrip();menu.Items.Add("Open SmartFiles",null,(_,_)=>open());menu.Items.Add("Exit",null,(_,_)=>exit());icon.ContextMenuStrip=menu;
  icon.DoubleClick+=(_,_)=>open();icon.BalloonTipClicked+=(_,_)=>open();
  timer=new(_=>Notify(),null,System.Threading.Timeout.Infinite,System.Threading.Timeout.Infinite);
  e.Updated+=Update;e.Ready+=Ready;Update();
 }
 void Update()=>System.Windows.Application.Current.Dispatcher.BeginInvoke(()=>{icon.Text=$"SmartFiles · {engine.Pending.Count} pending";});
 void Ready(FileTask task){lock(gate){if(count==0){oldest=task.SeenUtc;timer.Change(engine.Settings.Value.BatchWindowMs,System.Threading.Timeout.Infinite);}count++;}}
 void Notify(){int n;DateTime t;lock(gate){n=count;t=oldest;count=0;}
  if(!engine.Settings.Value.Notifications||(DateTime.UtcNow-t).TotalMilliseconds>engine.Settings.Value.InteractiveTtlMs)return;
  System.Windows.Application.Current.Dispatcher.BeginInvoke(()=>{
   var valid=engine.Pending.Count(x=>x.Status=="Pending"&&FileIdentity.Matches(x.Path,x.Stamp));
   if(valid>0)icon.ShowBalloonTip(2500,"SmartFiles",$"{Math.Min(n,valid)} new files have suggestions. Open to review; files move only after confirmation.",Forms.ToolTipIcon.Info);
  });
 }
 public void Dispose(){engine.Updated-=Update;engine.Ready-=Ready;timer.Dispose();icon.Visible=false;icon.Dispose();}
}
