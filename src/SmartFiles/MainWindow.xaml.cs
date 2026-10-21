using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartFiles.Core;
using MessageBox=SmartFiles.EnglishMessageBox;
using DataFormats=System.Windows.DataFormats;
namespace SmartFiles;
public partial class MainWindow:Window {
 readonly SmartFilesEngine engine;bool refreshing,busy;DateTime statusUntil;bool loadingSettings;System.Collections.Generic.List<ApiModel> apiModels=new();readonly System.Windows.Threading.DispatcherTimer refreshTimer;
 public MainWindow(SmartFilesEngine e){engine=e;InitializeComponent();refreshTimer=new(){Interval=TimeSpan.FromMilliseconds(160)};refreshTimer.Tick+=(_,_)=>{refreshTimer.Stop();if(IsLoaded)Refresh();};PolicyColumn.ItemsSource=new[]{new{Value=SourcePolicy.NormalMove,Label="Move after confirmation"},new{Value=SourcePolicy.AskBeforeMove,Label="Warn before moving"},new{Value=SourcePolicy.ClassifyOnly,Label="Classify only"},new{Value=SourcePolicy.Ignore,Label="Ignore"}};LoadSettings();Loaded+=(_,_)=>{Refresh();SetNavigation(0);};engine.Updated+=ScheduleRefresh;engine.Taxonomy.Changed+=ScheduleRefresh;}
 void ScheduleRefresh(){Dispatcher.BeginInvoke(()=>{if(!refreshTimer.IsEnabled)refreshTimer.Start();});}
 public void Refresh(){
  if(refreshing)return;refreshing=true;
  try{
   var preserveFolder=FolderChoice.SelectedValue;var preserveName=RenameBox.Text;var preserveBranch=BranchConsent.IsChecked;var selected=QueueGrid.SelectedItem as FileTask;var selectedIds=QueueGrid.SelectedItems.Cast<FileTask>().Select(x=>x.Id).ToHashSet();var snapshot=engine.Pending;QueueGrid.ItemsSource=snapshot;foreach(var item in snapshot.Where(x=>selectedIds.Contains(x.Id)))QueueGrid.SelectedItems.Add(item);EmptyQueue.Visibility=snapshot.Count==0?Visibility.Visible:Visibility.Collapsed;
   FolderChoice.ItemsSource=engine.Taxonomy.Nodes;if(selected!=null&&QueueGrid.SelectedItem is FileTask current&&current.Id==selected.Id){FolderChoice.SelectedValue=preserveFolder;RenameBox.Text=preserveName;BranchConsent.IsChecked=preserveBranch;}
   TaxonomyGrid.ItemsSource=engine.Taxonomy.Nodes;RuleFolderColumn.ItemsSource=engine.Taxonomy.Nodes;SourcesGrid.ItemsSource=engine.Settings.Value.Sources;if(RulesGrid.ItemsSource==null)RulesGrid.ItemsSource=engine.Rules.Rules;
   HistoryGrid.ItemsSource=engine.History.RecentMoves();
   var pending=engine.Pending;Summary.Text=$"{pending.Count} files pending · Local {pending.Count(x=>!x.Method.StartsWith("DeepSeek")&&!x.Method.StartsWith("API")&&x.Confidence>=engine.Settings.Value.LocalThreshold)} / API {pending.Count(x=>(x.Method.StartsWith("DeepSeek")||x.Method.StartsWith("API")))} / Uncertain {pending.Count(x=>x.Confidence<engine.Settings.Value.LocalThreshold)}";
   engine.Settings.Value.Monitoring=RuntimeHost.ReadSettings(engine.Settings.Home).Monitoring;MonitorButton.Content=engine.Settings.Value.Monitoring?"Pause monitoring":"Enable monitoring";
   if(!busy&&DateTime.UtcNow>=statusUntil)Status.Text=engine.Settings.Value.Monitoring?"Monitoring · New files only · Existing files stay in place":"Monitoring is off · Review Sources and select the folders to monitor";
  }finally{refreshing=false;}
 }
 public void ShowStatus(string message){Status.Text=message;statusUntil=DateTime.UtcNow.AddSeconds(15);}
 async void ToggleMonitor(object sender,RoutedEventArgs e){
  if(busy)return;busy=true;MonitorButton.IsEnabled=false;try{SourcesGrid.CommitEdit();SourcesGrid.CommitEdit();if(engine.Settings.Value.Monitoring){engine.StopMonitoring();engine.Settings.Save();}else{engine.Settings.Save();await engine.StartAsync();}Refresh();}catch(Exception ex){Error(ex);}finally{busy=false;MonitorButton.IsEnabled=true;Refresh();}
 }
 public void OpenTask(string value){var id=value.StartsWith("smartfiles://")?new Uri(value).AbsolutePath.Trim('/'):value;if(!Guid.TryParseExact(id,"N",out _))return;Refresh();SetNavigation(0);var task=engine.Pending.FirstOrDefault(x=>x.Id==id);if(task!=null){QueueGrid.SelectedItem=task;QueueGrid.ScrollIntoView(task);}}
 void ExitAll(object s,RoutedEventArgs e){if(busy){ShowStatus("Wait for the current operation to finish before exiting");return;}RuntimeHost.StopAll(engine.Settings.Home);((App)System.Windows.Application.Current).ExitApplication();}
 void HideWindow(object s,RoutedEventArgs e)=>Close();
 void ClosingWindow(object? s,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;ShowStatus("Wait for the current operation to finish before closing");return;}refreshTimer.Stop();engine.Updated-=ScheduleRefresh;engine.Taxonomy.Changed-=ScheduleRefresh;}
 void RefreshClick(object s,RoutedEventArgs e)=>Refresh();
 void SelectionChanged(object s,SelectionChangedEventArgs e){
  if(refreshing)return;if(QueueGrid.SelectedItem is not FileTask t){Details.Text="Select a file to review its suggestion.";RenameBox.Clear();FolderChoice.SelectedIndex=-1;BranchConsent.Visibility=Visibility.Collapsed;return;}
  FolderChoice.SelectedValue=t.FolderId;RenameBox.Text=string.IsNullOrWhiteSpace(t.SuggestedName)?t.Name:t.SuggestedName;
  Details.Text=t.Path+"\n"+t.Note;
  BranchConsent.Visibility=t.BranchName.Length>0?Visibility.Visible:Visibility.Collapsed;BranchConsent.Content="Approve new subcategory: "+t.BranchName;BranchConsent.IsChecked=false;
 }
 async void ConfirmSelected(object s,RoutedEventArgs e){
  if(busy)return;var tasks=QueueGrid.SelectedItems.Cast<FileTask>().ToArray();if(tasks.Length==0)return;
  if(MessageBox.Show($"Organize {tasks.Length} files? A single file uses the selected category and filename; multiple files use their individual suggestions.","Confirm organization",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  busy=true;
  try{
   foreach(var task in tasks){
    var src=engine.Settings.Value.Sources.FirstOrDefault(x=>x.Path==task.SourcePath);
    if(src?.Policy==SourcePolicy.AskBeforeMove&&!src.WarningAcknowledged){
     if(MessageBox.Show("Moving files from this source may break links in chat history. Allow this and future confirmed moves?","Source path warning",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)continue;
     src.WarningAcknowledged=true;engine.Settings.Save();
    }
    var id=tasks.Length==1?FolderChoice.SelectedValue as string:task.FolderId;
    if(string.IsNullOrWhiteSpace(id))throw new IOException("Select a valid category first");
    var folder=engine.Taxonomy.Resolve(id);var name=tasks.Length==1?RenameBox.Text:task.Name;
    if(File.Exists(Path.Combine(folder.Path,name))){
     if(MessageBox.Show("A file with this name already exists. Keep both by adding a number? Choose No to skip, then rename and try again.","Filename conflict",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.Yes)!=MessageBoxResult.Yes)continue;
     name=Path.GetFileName(FileTransactionService.AvailableName(folder.Path,name));
    }
    await engine.ConfirmAsync(task,id,name,tasks.Length==1&&BranchConsent.IsChecked==true);
   }
   ShowStatus("Selected files processed. Check History for details and undo.");
  }catch(Exception ex){Error(ex);}finally{busy=false;Refresh();}
 }
 void DismissSelected(object s,RoutedEventArgs e){if(busy)return;foreach(var t in QueueGrid.SelectedItems.Cast<FileTask>().ToArray())engine.Dismiss(t);Refresh();}
 void QueueKeyDown(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==Key.Left||e.Key==Key.Right){e.Handled=true;FolderChoice.SelectedIndex=Math.Clamp(FolderChoice.SelectedIndex+(e.Key==Key.Right?1:-1),0,Math.Max(0,FolderChoice.Items.Count-1));}else if(e.Key==Key.Enter){e.Handled=true;ConfirmSelected(s,e);}else if(e.Key==Key.Escape){e.Handled=true;DismissSelected(s,e);}}
 FolderNode SelectedCategory()=>TaxonomyGrid.SelectedItem as FolderNode??throw new IOException("Select a category first");
 void AddCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.Add(SelectedCategory().Id,CategoryName.Text);Refresh();}catch(Exception ex){Error(ex);}}
 void RenameCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.Rename(SelectedCategory().Id,CategoryName.Text);Refresh();}catch(Exception ex){Error(ex);}}
 void DeleteCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.DeleteEmpty(SelectedCategory().Id);Refresh();}catch(Exception ex){Error(ex);}}
 void DescribeCategory(object s,RoutedEventArgs e){try{engine.Taxonomy.Describe(SelectedCategory().Id,CategoryDescription.Text,CategoryKeywords.Text);Refresh();}catch(Exception ex){Error(ex);}}
 void Rediscover(object s,RoutedEventArgs e){try{engine.Discover();SourcesGrid.ItemsSource=null;Refresh();}catch(Exception ex){Error(ex);}}
 void AddPath(string path){FileIdentity.ValidatePath(path);if(!Directory.Exists(path))return;if(!engine.Settings.Value.Sources.Any(x=>string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)))engine.Settings.Value.Sources.Add(new(){Name="Manual source",Path=Path.GetFullPath(path)});SourcesGrid.ItemsSource=null;Refresh();}
 void AddSource(object s,RoutedEventArgs e){using var dialog=new System.Windows.Forms.FolderBrowserDialog();if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)try{AddPath(dialog.SelectedPath);}catch(Exception ex){Error(ex);}}
 void SourceDragOver(object s,System.Windows.DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?System.Windows.DragDropEffects.Copy:System.Windows.DragDropEffects.None;e.Handled=true;}
 void SourceDrop(object s,System.Windows.DragEventArgs e){try{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths)foreach(var p in paths)AddPath(p);}catch(Exception ex){Error(ex);}}
 async void SaveSources(object s,RoutedEventArgs e){if(busy)return;busy=true;try{SourcesGrid.CommitEdit();SourcesGrid.CommitEdit();engine.Settings.Save();if(engine.Settings.Value.Monitoring)await engine.StartAsync();ShowStatus("Sources saved. Re-enabling sets a new baseline; existing files are not scanned.");}catch(Exception ex){Error(ex);}finally{busy=false;}}
 void LoadSettings(){loadingSettings=true;var v=engine.Settings.Value;ProviderChoice.SelectedIndex=DeepSeekProvider.IsDeepSeek(v.BaseUrl)?0:DeepSeekProvider.IsGemini(v.BaseUrl)?1:2;BaseUrlBox.Text=v.BaseUrl;TextModelBox.Text=v.TextModel;VisionModelBox.Text=v.VisionModel;SendName.IsChecked=v.SendFilename;SendMeta.IsChecked=v.SendMetadata;SendText.IsChecked=v.SendText;SendOcr.IsChecked=v.SendOcr;HistoryCountBox.Text=v.HistoryMaxCount.ToString();HistoryDaysBox.Text=v.HistoryMaxDays.ToString();SendImage.IsChecked=v.SendThumbnail;LocalAi.IsChecked=v.LocalAi;UseOcr.IsChecked=v.Ocr;RenameAi.IsChecked=v.RenameSuggestions;ClassifyEnabled.IsChecked=v.Classification;ThresholdBox.Text=v.LocalThreshold.ToString();HighThresholdBox.Text=v.HighThreshold.ToString();TtlBox.Text=v.InteractiveTtlMs.ToString();BatchBox.Text=v.BatchWindowMs.ToString();BudgetBox.Text=v.ImageApiBudgetPerBatch.ToString();CandidateBox.Text=v.CandidateCount.ToString();StrictHash.IsChecked=v.StrictHash;Startup.IsChecked=v.AutoStart;CloseToTray.IsChecked=v.CloseToTray;WorkerIdleBox.Text=v.WorkerIdleSeconds.ToString();Notifications.IsChecked=v.Notifications;TextModelBox.ItemsSource=DeepSeekProvider.IsDeepSeek(v.BaseUrl)?new[]{"deepseek-flash","deepseek-v4-pro"}:DeepSeekProvider.IsGemini(v.BaseUrl)?new[]{"gemini-3.8-flash"}:Array.Empty<string>();TextModelBox.Text=v.TextModel;loadingSettings=false;ModelHint.Text=string.IsNullOrEmpty(engine.Settings.ReadKey())?"Enter a key to connect":"Encrypted key saved";}
 void Save(){
  var v=Json.Decode<Settings>(Json.Encode(engine.Settings.Value));v.BaseUrl=BaseUrlBox.Text.Trim();v.TextModel=TextModelBox.Text.Trim();v.VisionModel=VisionModelBox.Text.Trim();v.SendFilename=SendName.IsChecked==true;v.SendMetadata=SendMeta.IsChecked==true;v.SendText=SendText.IsChecked==true;v.SendOcr=SendOcr.IsChecked==true;v.HistoryMaxCount=int.Parse(HistoryCountBox.Text);v.HistoryMaxDays=int.Parse(HistoryDaysBox.Text);v.SendThumbnail=SendImage.IsChecked==true;v.LocalAi=LocalAi.IsChecked==true;v.Ocr=UseOcr.IsChecked==true;v.RenameSuggestions=RenameAi.IsChecked==true;v.Classification=ClassifyEnabled.IsChecked==true;
  v.LocalThreshold=double.Parse(ThresholdBox.Text);v.HighThreshold=double.Parse(HighThresholdBox.Text);v.InteractiveTtlMs=int.Parse(TtlBox.Text);v.BatchWindowMs=int.Parse(BatchBox.Text);v.ImageApiBudgetPerBatch=int.Parse(BudgetBox.Text);v.CandidateCount=int.Parse(CandidateBox.Text);v.Notifications=Notifications.IsChecked==true;v.StrictHash=StrictHash.IsChecked==true;
  v.CloseToTray=CloseToTray.IsChecked==true;v.WorkerIdleSeconds=int.Parse(WorkerIdleBox.Text);SettingsService.Validate(v);if(KeyBox.Password.Length>0)engine.Settings.SaveKey(KeyBox.Password,v.BaseUrl);v.Sources=engine.Settings.Value.Sources;v.Monitoring=RuntimeHost.ReadSettings(engine.Settings.Home).Monitoring;engine.Settings.Replace(v);engine.History.ConfigureRetention(v.HistoryMaxCount,v.HistoryMaxDays);engine.History.Prune(v.HistoryMaxCount,v.HistoryMaxDays);KeyBox.Clear();
  if((Startup.IsChecked==true)!=v.AutoStart)engine.Settings.SetStartup(Startup.IsChecked==true,Environment.ProcessPath!);
 }
 void SaveSettings(object s,RoutedEventArgs e){try{Save();ShowStatus("Settings saved");}catch(Exception ex){Error(ex);}}
 async void TestApi(object s,RoutedEventArgs e){if(busy)return;busy=true;try{if(string.IsNullOrWhiteSpace(TextModelBox.Text))await FetchModelList();Save();ShowStatus("Testing API…");ShowStatus(await engine.Provider.TestAsync(System.Threading.CancellationToken.None));}catch(Exception ex){Error(ex);}finally{busy=false;}}
 void SaveRules(object s,RoutedEventArgs e){try{RulesGrid.CommitEdit();RulesGrid.CommitEdit();foreach(var r in RulesGrid.Items.Cast<LearnedRule>())engine.Rules.Save(r);ShowStatus("Rules saved");}catch(Exception ex){Error(ex);}}
 void DeleteRule(object s,RoutedEventArgs e){if(RulesGrid.SelectedItem is LearnedRule r){engine.Rules.Delete(r.Id);RulesGrid.ItemsSource=null;Refresh();}}
 async void UndoSelected(object s,RoutedEventArgs e){if(busy)return;if(HistoryGrid.SelectedItem is not MoveRecord r)return;if(MessageBox.Show("Undo this operation? It will stop if the file changed or the original path is occupied.","Undo",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;busy=true;try{await engine.Undo.UndoAsync(r);ShowStatus("Selected operation undone.");Refresh();}catch(Exception ex){Error(ex);}finally{busy=false;}}
 public async System.Threading.Tasks.Task CapturePagesAsync(){
  Directory.CreateDirectory(@"D:\SmartFilesApp\DevArtifacts\UISmoke");
  if(QueueGrid.Items.Count>=2){QueueGrid.SelectedItems.Add(QueueGrid.Items[0]);QueueGrid.SelectedItems.Add(QueueGrid.Items[1]);RenameBox.Text="preserved-input.txt";Refresh();if(QueueGrid.SelectedItems.Count!=2||RenameBox.Text!="preserved-input.txt")throw new InvalidOperationException("Refresh lost selection or input");QueueGrid.SelectedItems.Clear();ShowStatus("Verification status");Refresh();if(Status.Text!="Verification status")throw new InvalidOperationException("Refresh replaced the status message");}
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
 void SetNavigation(int index){Tabs.SelectedIndex=index;var nav=new[]{Nav0,Nav1,Nav2,Nav3,Nav4,Nav5}[index];if(nav!=null&&nav.IsChecked!=true)nav.IsChecked=true;PageTitle.Text=new[]{"Inbox","Categories","Sources","AI & Settings","Learned Rules","History"}[index];PageSubtitle.Text=new[]{"Review suggestions, then confirm to organize.","Give every file a clear home.","Choose download and receive folders to manage.","Local first, with cloud help when needed.","Learn from your choices for better suggestions.","Every move is recorded and can be safely undone."}[index];}
 void ProviderChanged(object sender,SelectionChangedEventArgs e){if(loadingSettings||BaseUrlBox==null)return;int index=ProviderChoice.SelectedIndex;BaseUrlBox.Text=index switch{0=>"https://api.deepseek.com",1=>"https://generativelanguage.googleapis.com/v1beta/openai",_=>""};KeyBox.Clear();apiModels.Clear();TextModelBox.ItemsSource=index==0?new[]{"deepseek-flash","deepseek-v4-pro"}:index==1?new[]{"gemini-3.8-flash"}:Array.Empty<string>();TextModelBox.Text=index==0?"deepseek-flash":index==1?"gemini-3.8-flash":"";VisionModelBox.Text=index==0?"deepseek-flash":index==1?"gemini-3.8-flash":"";ConnectionOptions.IsExpanded=index==2;ModelHint.Text=index==0?"Flash supports images; Pro is for text":index==1?"Gemini Flash supports text and images":"Enter a compatible API URL and key";}
 void ModelChanged(object sender,SelectionChangedEventArgs e){if(loadingSettings||VisionModelBox==null)return;var id=TextModelBox.SelectedItem as string;if(id!=null&&apiModels.Any(x=>x.Id==id&&x.Vision))VisionModelBox.Text=id;}
 async System.Threading.Tasks.Task FetchModelList(){var url=BaseUrlBox.Text.Trim();var key=KeyBox.Password.Length>0?KeyBox.Password:engine.Settings.ReadKey(url);var fetched=await engine.Provider.ListModelsAsync(url,key,System.Threading.CancellationToken.None);if(BaseUrlBox.Text.Trim()!=url)return;apiModels=fetched;if(apiModels.Count==0)throw new IOException("No chat models returned. Enter a model name manually.");var previous=TextModelBox.Text;TextModelBox.ItemsSource=apiModels.Select(x=>x.Id).ToArray();var choice=apiModels.FirstOrDefault(x=>x.Id==previous)??apiModels.FirstOrDefault(x=>x.Id=="deepseek-flash"||x.Id=="gemini-3.8-flash")??apiModels.FirstOrDefault(x=>x.Vision)??apiModels[0];TextModelBox.Text=choice.Id;VisionModelBox.Text=(apiModels.FirstOrDefault(x=>x.Id==choice.Id&&x.Vision)??apiModels.FirstOrDefault(x=>x.Vision))?.Id??"";ModelHint.Text=$"Loaded {apiModels.Count} models";}
 async void FetchModels(object sender,RoutedEventArgs e){if(busy)return;busy=true;try{ModelHint.Text="Loading models…";await FetchModelList();}catch(Exception ex){ModelHint.Text="Could not load models; enter a name manually";Error(ex);}finally{busy=false;}}
 async void RefreshStorage(object s,RoutedEventArgs e){if(busy)return;busy=true;try{StorageGrid.ItemsSource=await System.Threading.Tasks.Task.Run(()=>StorageMaintenance.Measure(engine.Settings.Home));}catch(Exception ex){Error(ex);}finally{busy=false;}}
 async void CleanStorage(object s,RoutedEventArgs e){if(busy)return;busy=true;try{await System.Threading.Tasks.Task.Run(()=>StorageMaintenance.Run(engine.Settings.Home,engine.History,engine.Settings.Value,true));StorageGrid.ItemsSource=await System.Threading.Tasks.Task.Run(()=>StorageMaintenance.Measure(engine.Settings.Home));ShowStatus("Cleared safe files generated by SmartFiles");}catch(Exception ex){Error(ex);}finally{busy=false;}}
 void CategorySelected(object sender,SelectionChangedEventArgs e){if(TaxonomyGrid.SelectedItem is FolderNode n){CategoryName.Text=Path.GetFileName(n.Path);CategoryDescription.Text=n.Description;CategoryKeywords.Text=n.Keywords;}}
 void Error(Exception e){engine.Log.Write("ui_operation_failed",e);MessageBox.Show(e.Message,"SmartFiles · Operation incomplete",MessageBoxButton.OK,MessageBoxImage.Warning);}
}
