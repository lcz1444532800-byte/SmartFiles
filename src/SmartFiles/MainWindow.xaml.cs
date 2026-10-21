using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartFiles.Core;
using MessageBox=System.Windows.MessageBox;
using DataFormats=System.Windows.DataFormats;
namespace SmartFiles;
public partial class MainWindow:Window {
 readonly SmartFilesEngine engine;bool refreshing,busy;DateTime statusUntil;bool loadingSettings;System.Collections.Generic.List<ApiModel> apiModels=new();readonly System.Windows.Threading.DispatcherTimer refreshTimer;
 public MainWindow(SmartFilesEngine e){engine=e;InitializeComponent();refreshTimer=new(){Interval=TimeSpan.FromMilliseconds(160)};refreshTimer.Tick+=(_,_)=>{refreshTimer.Stop();if(IsLoaded)Refresh();};PolicyColumn.ItemsSource=new[]{new{Value=SourcePolicy.NormalMove,Label="确认后移动"},new{Value=SourcePolicy.AskBeforeMove,Label="移动前提醒"},new{Value=SourcePolicy.ClassifyOnly,Label="只分类"},new{Value=SourcePolicy.Ignore,Label="忽略"}};LoadSettings();Loaded+=(_,_)=>{Refresh();SetNavigation(0);};engine.Updated+=ScheduleRefresh;engine.Taxonomy.Changed+=ScheduleRefresh;}
 void ScheduleRefresh(){Dispatcher.BeginInvoke(()=>{if(!refreshTimer.IsEnabled)refreshTimer.Start();});}
 public void Refresh(){
  if(refreshing)return;refreshing=true;
  try{
   var preserveFolder=FolderChoice.SelectedValue;var preserveName=RenameBox.Text;var preserveBranch=BranchConsent.IsChecked;var selected=QueueGrid.SelectedItem as FileTask;var selectedIds=QueueGrid.SelectedItems.Cast<FileTask>().Select(x=>x.Id).ToHashSet();var snapshot=engine.Pending;QueueGrid.ItemsSource=snapshot;foreach(var item in snapshot.Where(x=>selectedIds.Contains(x.Id)))QueueGrid.SelectedItems.Add(item);EmptyQueue.Visibility=snapshot.Count==0?Visibility.Visible:Visibility.Collapsed;
   FolderChoice.ItemsSource=engine.Taxonomy.Nodes;if(selected!=null&&QueueGrid.SelectedItem is FileTask current&&current.Id==selected.Id){FolderChoice.SelectedValue=preserveFolder;RenameBox.Text=preserveName;BranchConsent.IsChecked=preserveBranch;}
   TaxonomyGrid.ItemsSource=engine.Taxonomy.Nodes;RuleFolderColumn.ItemsSource=engine.Taxonomy.Nodes;SourcesGrid.ItemsSource=engine.Settings.Value.Sources;if(RulesGrid.ItemsSource==null)RulesGrid.ItemsSource=engine.Rules.Rules;
   HistoryGrid.ItemsSource=engine.History.RecentMoves();
   var pending=engine.Pending;Summary.Text=$"{pending.Count} 个文件待处理 · 本地 {pending.Count(x=>!x.Method.StartsWith("DeepSeek")&&!x.Method.StartsWith("API")&&x.Confidence>=engine.Settings.Value.LocalThreshold)} / API {pending.Count(x=>(x.Method.StartsWith("DeepSeek")||x.Method.StartsWith("API")))} / 不确定 {pending.Count(x=>x.Confidence<engine.Settings.Value.LocalThreshold)}";
   engine.Settings.Value.Monitoring=RuntimeHost.ReadSettings(engine.Settings.Home).Monitoring;MonitorButton.Content=engine.Settings.Value.Monitoring?"暂停监控":"启用监控";
   if(!busy&&DateTime.UtcNow>=statusUntil)Status.Text=engine.Settings.Value.Monitoring?"监控中 · 只处理新文件 · 旧文件不动":"监控未启用 · 请先检查「文件来源」并勾选管理范围";
  }finally{refreshing=false;}
 }
 public void ShowStatus(string message){Status.Text=message;statusUntil=DateTime.UtcNow.AddSeconds(15);}
 async void ToggleMonitor(object sender,RoutedEventArgs e){
  if(busy)return;busy=true;MonitorButton.IsEnabled=false;try{SourcesGrid.CommitEdit();SourcesGrid.CommitEdit();if(engine.Settings.Value.Monitoring){engine.StopMonitoring();engine.Settings.Save();}else{engine.Settings.Save();await engine.StartAsync();}Refresh();}catch(Exception ex){Error(ex);}finally{busy=false;MonitorButton.IsEnabled=true;Refresh();}
 }
 public void OpenTask(string value){var id=value.StartsWith("smartfiles://")?new Uri(value).AbsolutePath.Trim('/'):value;if(!Guid.TryParseExact(id,"N",out _))return;Refresh();SetNavigation(0);var task=engine.Pending.FirstOrDefault(x=>x.Id==id);if(task!=null){QueueGrid.SelectedItem=task;QueueGrid.ScrollIntoView(task);}}
 void ExitAll(object s,RoutedEventArgs e){if(busy){ShowStatus("当前操作完成后即可完全退出");return;}RuntimeHost.StopAll(engine.Settings.Home);((App)System.Windows.Application.Current).ExitApplication();}
 void HideWindow(object s,RoutedEventArgs e)=>Close();
 void ClosingWindow(object? s,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;ShowStatus("当前操作完成后即可关闭界面");return;}refreshTimer.Stop();engine.Updated-=ScheduleRefresh;engine.Taxonomy.Changed-=ScheduleRefresh;}
 void RefreshClick(object s,RoutedEventArgs e)=>Refresh();
 void SelectionChanged(object s,SelectionChangedEventArgs e){
  if(refreshing)return;if(QueueGrid.SelectedItem is not FileTask t){Details.Text="选择文件以检查推荐。";RenameBox.Clear();FolderChoice.SelectedIndex=-1;BranchConsent.Visibility=Visibility.Collapsed;return;}
  FolderChoice.SelectedValue=t.FolderId;RenameBox.Text=string.IsNullOrWhiteSpace(t.SuggestedName)?t.Name:t.SuggestedName;
  Details.Text=t.Path+"\n"+t.Note;
  BranchConsent.Visibility=t.BranchName.Length>0?Visibility.Visible:Visibility.Collapsed;BranchConsent.Content="同意新建子分类："+t.BranchName;BranchConsent.IsChecked=false;
 }
 async void ConfirmSelected(object s,RoutedEventArgs e){
  if(busy)return;var tasks=QueueGrid.SelectedItems.Cast<FileTask>().ToArray();if(tasks.Length==0)return;
  if(MessageBox.Show($"将按当前推荐整理 {tasks.Length} 个文件。单文件使用下方选择的分类和名称；多文件使用各自推荐。是否继续？","确认整理",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  busy=true;
  try{
   foreach(var task in tasks){
    var src=engine.Settings.Value.Sources.FirstOrDefault(x=>x.Path==task.SourcePath);
    if(src?.Policy==SourcePolicy.AskBeforeMove&&!src.WarningAcknowledged){
     if(MessageBox.Show("移动该来源的文件可能导致聊天软件中的历史打开路径失效。是否允许本次及今后经确认的移动？","来源路径提醒",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)continue;
     src.WarningAcknowledged=true;engine.Settings.Save();
    }
    var id=tasks.Length==1?FolderChoice.SelectedValue as string:task.FolderId;
    if(string.IsNullOrWhiteSpace(id))throw new IOException("请先选择有效分类");
    var folder=engine.Taxonomy.Resolve(id);var name=tasks.Length==1?RenameBox.Text:task.Name;
    if(File.Exists(Path.Combine(folder.Path,name))){
     if(MessageBox.Show("目标已有同名文件。是否保留两个（自动增加序号）？选择“否”取消此项，可修改名称后重试。","同名冲突",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.Yes)!=MessageBoxResult.Yes)continue;
     name=Path.GetFileName(FileTransactionService.AvailableName(folder.Path,name));
    }
    await engine.ConfirmAsync(task,id,name,tasks.Length==1&&BranchConsent.IsChecked==true);
   }
   ShowStatus("选中项处理完成。可以在「历史 / 撤销」检查。");
  }catch(Exception ex){Error(ex);}finally{busy=false;Refresh();}
 }
 void DismissSelected(object s,RoutedEventArgs e){if(busy)return;foreach(var t in QueueGrid.SelectedItems.Cast<FileTask>().ToArray())engine.Dismiss(t);Refresh();}
 void QueueKeyDown(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==Key.Left||e.Key==Key.Right){e.Handled=true;FolderChoice.SelectedIndex=Math.Clamp(FolderChoice.SelectedIndex+(e.Key==Key.Right?1:-1),0,Math.Max(0,FolderChoice.Items.Count-1));}else if(e.Key==Key.Enter){e.Handled=true;ConfirmSelected(s,e);}else if(e.Key==Key.Escape){e.Handled=true;DismissSelected(s,e);}}
 FolderNode SelectedCategory()=>TaxonomyGrid.SelectedItem as FolderNode??throw new IOException("先选择一个分类");
 void AddCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.Add(SelectedCategory().Id,CategoryName.Text);Refresh();}catch(Exception ex){Error(ex);}}
 void RenameCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.Rename(SelectedCategory().Id,CategoryName.Text);Refresh();}catch(Exception ex){Error(ex);}}
 void DeleteCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.DeleteEmpty(SelectedCategory().Id);Refresh();}catch(Exception ex){Error(ex);}}
 void DescribeCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.Describe(SelectedCategory().Id,CategoryDescription.Text,CategoryKeywords.Text);Refresh();}catch(Exception ex){Error(ex);}}
 void Rediscover(object s,RoutedEventArgs e){try{engine.Discover();SourcesGrid.ItemsSource=null;Refresh();}catch(Exception ex){Error(ex);}}
 void AddPath(string path){FileIdentity.ValidatePath(path);if(!Directory.Exists(path))return;if(!engine.Settings.Value.Sources.Any(x=>string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)))engine.Settings.Value.Sources.Add(new(){Name="手动来源",Path=Path.GetFullPath(path)});SourcesGrid.ItemsSource=null;Refresh();}
 void AddSource(object s,RoutedEventArgs e){using var dialog=new System.Windows.Forms.FolderBrowserDialog();if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)try{AddPath(dialog.SelectedPath);}catch(Exception ex){Error(ex);}}
 void SourceDragOver(object s,System.Windows.DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?System.Windows.DragDropEffects.Copy:System.Windows.DragDropEffects.None;e.Handled=true;}
 void SourceDrop(object s,System.Windows.DragEventArgs e){try{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths)foreach(var p in paths)AddPath(p);}catch(Exception ex){Error(ex);}}
 async void SaveSources(object s,RoutedEventArgs e){if(busy)return;busy=true;try{SourcesGrid.CommitEdit();SourcesGrid.CommitEdit();engine.Settings.Save();if(engine.Settings.Value.Monitoring)await engine.StartAsync();ShowStatus("来源已保存。重新启用以当前已有文件为基线，不补扫旧文件。");}catch(Exception ex){Error(ex);}finally{busy=false;}}
 void LoadSettings(){loadingSettings=true;var v=engine.Settings.Value;ProviderChoice.SelectedIndex=DeepSeekProvider.IsDeepSeek(v.BaseUrl)?0:DeepSeekProvider.IsGemini(v.BaseUrl)?1:2;BaseUrlBox.Text=v.BaseUrl;TextModelBox.Text=v.TextModel;VisionModelBox.Text=v.VisionModel;SendName.IsChecked=v.SendFilename;SendMeta.IsChecked=v.SendMetadata;SendText.IsChecked=v.SendText;SendOcr.IsChecked=v.SendOcr;HistoryCountBox.Text=v.HistoryMaxCount.ToString();HistoryDaysBox.Text=v.HistoryMaxDays.ToString();SendImage.IsChecked=v.SendThumbnail;LocalAi.IsChecked=v.LocalAi;UseOcr.IsChecked=v.Ocr;RenameAi.IsChecked=v.RenameSuggestions;ClassifyEnabled.IsChecked=v.Classification;ThresholdBox.Text=v.LocalThreshold.ToString();HighThresholdBox.Text=v.HighThreshold.ToString();TtlBox.Text=v.InteractiveTtlMs.ToString();BatchBox.Text=v.BatchWindowMs.ToString();BudgetBox.Text=v.ImageApiBudgetPerBatch.ToString();CandidateBox.Text=v.CandidateCount.ToString();StrictHash.IsChecked=v.StrictHash;Startup.IsChecked=v.AutoStart;CloseToTray.IsChecked=v.CloseToTray;WorkerIdleBox.Text=v.WorkerIdleSeconds.ToString();Notifications.IsChecked=v.Notifications;TextModelBox.ItemsSource=DeepSeekProvider.IsDeepSeek(v.BaseUrl)?new[]{"deepseek-flash","deepseek-v4-pro"}:DeepSeekProvider.IsGemini(v.BaseUrl)?new[]{"gemini-3.8-flash"}:Array.Empty<string>();TextModelBox.Text=v.TextModel;loadingSettings=false;ModelHint.Text=string.IsNullOrEmpty(engine.Settings.ReadKey())?"填入密钥即可连接":"已有加密保存的密钥";}
 void Save(){
  var v=Json.Decode<Settings>(Json.Encode(engine.Settings.Value));v.BaseUrl=BaseUrlBox.Text.Trim();v.TextModel=TextModelBox.Text.Trim();v.VisionModel=VisionModelBox.Text.Trim();v.SendFilename=SendName.IsChecked==true;v.SendMetadata=SendMeta.IsChecked==true;v.SendText=SendText.IsChecked==true;v.SendOcr=SendOcr.IsChecked==true;v.HistoryMaxCount=int.Parse(HistoryCountBox.Text);v.HistoryMaxDays=int.Parse(HistoryDaysBox.Text);v.SendThumbnail=SendImage.IsChecked==true;v.LocalAi=LocalAi.IsChecked==true;v.Ocr=UseOcr.IsChecked==true;v.RenameSuggestions=RenameAi.IsChecked==true;v.Classification=ClassifyEnabled.IsChecked==true;
  v.LocalThreshold=double.Parse(ThresholdBox.Text);v.HighThreshold=double.Parse(HighThresholdBox.Text);v.InteractiveTtlMs=int.Parse(TtlBox.Text);v.BatchWindowMs=int.Parse(BatchBox.Text);v.ImageApiBudgetPerBatch=int.Parse(BudgetBox.Text);v.CandidateCount=int.Parse(CandidateBox.Text);v.Notifications=Notifications.IsChecked==true;v.StrictHash=StrictHash.IsChecked==true;
  v.CloseToTray=CloseToTray.IsChecked==true;v.WorkerIdleSeconds=int.Parse(WorkerIdleBox.Text);SettingsService.Validate(v);if(KeyBox.Password.Length>0)engine.Settings.SaveKey(KeyBox.Password,v.BaseUrl);v.Sources=engine.Settings.Value.Sources;v.Monitoring=RuntimeHost.ReadSettings(engine.Settings.Home).Monitoring;engine.Settings.Replace(v);engine.History.ConfigureRetention(v.HistoryMaxCount,v.HistoryMaxDays);engine.History.Prune(v.HistoryMaxCount,v.HistoryMaxDays);KeyBox.Clear();
  if((Startup.IsChecked==true)!=v.AutoStart)engine.Settings.SetStartup(Startup.IsChecked==true,Environment.ProcessPath!);
 }
 void SaveSettings(object s,RoutedEventArgs e){try{Save();ShowStatus("设置已保存");}catch(Exception ex){Error(ex);}}
 async void TestApi(object s,RoutedEventArgs e){if(busy)return;busy=true;try{if(string.IsNullOrWhiteSpace(TextModelBox.Text))await FetchModelList();Save();ShowStatus("正在测试 API…");ShowStatus(await engine.Provider.TestAsync(System.Threading.CancellationToken.None));}catch(Exception ex){Error(ex);}finally{busy=false;}}
 void SaveRules(object s,RoutedEventArgs e){try{RulesGrid.CommitEdit();RulesGrid.CommitEdit();foreach(var r in RulesGrid.Items.Cast<LearnedRule>())engine.Rules.Save(r);ShowStatus("规则已保存");}catch(Exception ex){Error(ex);}}
 void DeleteRule(object s,RoutedEventArgs e){if(RulesGrid.SelectedItem is LearnedRule r){engine.Rules.Delete(r.Id);RulesGrid.ItemsSource=null;Refresh();}}
 async void UndoSelected(object s,RoutedEventArgs e){if(busy)return;if(HistoryGrid.SelectedItem is not MoveRecord r)return;if(MessageBox.Show("确认撤销此项？文件已变化或原路径被占用时会停止。","撤销",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;busy=true;try{await engine.Undo.UndoAsync(r);ShowStatus("已撤销选中的整理记录。");Refresh();}catch(Exception ex){Error(ex);}finally{busy=false;}}
 public async System.Threading.Tasks.Task CapturePagesAsync(){
  Directory.CreateDirectory(@"D:\SmartFilesApp\DevArtifacts\UISmoke");
  if(QueueGrid.Items.Count>=2){QueueGrid.SelectedItems.Add(QueueGrid.Items[0]);QueueGrid.SelectedItems.Add(QueueGrid.Items[1]);RenameBox.Text="保留用户输入.txt";Refresh();if(QueueGrid.SelectedItems.Count!=2||RenameBox.Text!="保留用户输入.txt")throw new InvalidOperationException("刷新丢失用户选择或输入");QueueGrid.SelectedItems.Clear();ShowStatus("验收状态提示");Refresh();if(Status.Text!="验收状态提示")throw new InvalidOperationException("刷新覆盖状态提示");}
  for(int i=0;i<Tabs.Items.Count;i++){
   SetNavigation(i);Refresh();await System.Threading.Tasks.Task.Delay(200);UpdateLayout();
   var surface=(FrameworkElement)Content;var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
   bitmap.Render(surface);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
   using var stream=File.Create($@"D:\SmartFilesApp\DevArtifacts\UISmoke\page-{i}.png");encoder.Save(stream);
  }
  SetNavigation(3);Width=MinWidth;Height=MinHeight;await System.Threading.Tasks.Task.Delay(200);UpdateLayout();var small=(FrameworkElement)Content;var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)small.ActualWidth,(int)small.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);shot.Render(small);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));using(var file=File.Create(@"D:\SmartFilesApp\DevArtifacts\UISmoke\settings-small.png"))png.Save(file);
  File.WriteAllText(@"D:\SmartFilesApp\DevArtifacts\ui-interactions.json",Json.Encode(new{multiSelectionPreserved=true,draftPreserved=true,statusPreserved=true,pagesCaptured=Tabs.Items.Count}));SetNavigation(0);
 }
 void Navigate(object sender,RoutedEventArgs e){if(Tabs==null)return;int index=int.Parse((string)((System.Windows.Controls.RadioButton)sender).Tag);SetNavigation(index);}
 void SetNavigation(int index){Tabs.SelectedIndex=index;var nav=new[]{Nav0,Nav1,Nav2,Nav3,Nav4,Nav5}[index];if(nav!=null&&nav.IsChecked!=true)nav.IsChecked=true;PageTitle.Text=new[]{"待整理","分类管理","文件来源","AI 与设置","学习规则","整理记录"}[index];PageSubtitle.Text=new[]{"检查推荐，确认后轻松归档。","为文件建立清晰的归属。","选择要管理的下载与接收文件夹。","本地优先，云端按需协助。","记住你的选择，让推荐更贴近习惯。","每一次整理都有记录，支持安全撤销。"}[index];}
 void ProviderChanged(object sender,SelectionChangedEventArgs e){if(loadingSettings||BaseUrlBox==null)return;int index=ProviderChoice.SelectedIndex;BaseUrlBox.Text=index switch{0=>"https://api.deepseek.com",1=>"https://generativelanguage.googleapis.com/v1beta/openai",_=>""};KeyBox.Clear();apiModels.Clear();TextModelBox.ItemsSource=index==0?new[]{"deepseek-flash","deepseek-v4-pro"}:index==1?new[]{"gemini-3.8-flash"}:Array.Empty<string>();TextModelBox.Text=index==0?"deepseek-flash":index==1?"gemini-3.8-flash":"";VisionModelBox.Text=index==0?"deepseek-flash":index==1?"gemini-3.8-flash":"";ConnectionOptions.IsExpanded=index==2;ModelHint.Text=index==0?"Flash 支持图片；Pro 用于文本":index==1?"Gemini Flash 支持文本与图片":"填写兼容接口地址和密钥";}
 void ModelChanged(object sender,SelectionChangedEventArgs e){if(loadingSettings||VisionModelBox==null)return;var id=TextModelBox.SelectedItem as string;if(id!=null&&apiModels.Any(x=>x.Id==id&&x.Vision))VisionModelBox.Text=id;}
 async System.Threading.Tasks.Task FetchModelList(){var url=BaseUrlBox.Text.Trim();var key=KeyBox.Password.Length>0?KeyBox.Password:engine.Settings.ReadKey(url);var fetched=await engine.Provider.ListModelsAsync(url,key,System.Threading.CancellationToken.None);if(BaseUrlBox.Text.Trim()!=url)return;apiModels=fetched;if(apiModels.Count==0)throw new IOException("服务商没有返回可用聊天模型，可直接输入模型名称");var previous=TextModelBox.Text;TextModelBox.ItemsSource=apiModels.Select(x=>x.Id).ToArray();var choice=apiModels.FirstOrDefault(x=>x.Id==previous)??apiModels.FirstOrDefault(x=>x.Id=="deepseek-flash"||x.Id=="gemini-3.8-flash")??apiModels.FirstOrDefault(x=>x.Vision)??apiModels[0];TextModelBox.Text=choice.Id;VisionModelBox.Text=(apiModels.FirstOrDefault(x=>x.Id==choice.Id&&x.Vision)??apiModels.FirstOrDefault(x=>x.Vision))?.Id??"";ModelHint.Text=$"已获取 {apiModels.Count} 个模型";}
 async void FetchModels(object sender,RoutedEventArgs e){if(busy)return;busy=true;try{ModelHint.Text="正在获取模型…";await FetchModelList();}catch(Exception ex){ModelHint.Text="获取失败，可手动填写模型";Error(ex);}finally{busy=false;}}
 async void RefreshStorage(object s,RoutedEventArgs e){if(busy)return;busy=true;try{StorageGrid.ItemsSource=await System.Threading.Tasks.Task.Run(()=>StorageMaintenance.Measure(engine.Settings.Home));}catch(Exception ex){Error(ex);}finally{busy=false;}}
 async void CleanStorage(object s,RoutedEventArgs e){if(busy)return;busy=true;try{await System.Threading.Tasks.Task.Run(()=>StorageMaintenance.Run(engine.Settings.Home,engine.History,engine.Settings.Value,true));StorageGrid.ItemsSource=await System.Threading.Tasks.Task.Run(()=>StorageMaintenance.Measure(engine.Settings.Home));ShowStatus("已清理 SmartFiles 自己生成的安全缓存");}catch(Exception ex){Error(ex);}finally{busy=false;}}
 void CategorySelected(object sender,SelectionChangedEventArgs e){if(TaxonomyGrid.SelectedItem is FolderNode n){CategoryName.Text=Path.GetFileName(n.Path);CategoryDescription.Text=n.Description;CategoryKeywords.Text=n.Keywords;}}
 void Error(Exception e){engine.Log.Write("ui_operation_failed",e);MessageBox.Show(e.Message,"SmartFiles · 操作未完成",MessageBoxButton.OK,MessageBoxImage.Warning);}
}
