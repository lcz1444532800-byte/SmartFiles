using System.Windows;
using System.Windows.Controls;
using Button=System.Windows.Controls.Button;
using Orientation=System.Windows.Controls.Orientation;
using HorizontalAlignment=System.Windows.HorizontalAlignment;
namespace SmartFiles;
public static class EnglishMessageBox {
 public static MessageBoxResult Show(string text,string caption,MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage icon=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.None){
  var result=MessageBoxResult.No;
  var dialog=new Window{Title=caption,Width=520,SizeToContent=SizeToContent.Height,MaxHeight=650,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen};
  var owner=System.Windows.Application.Current?.MainWindow;
  if(owner?.IsVisible==true){dialog.Owner=owner;dialog.WindowStartupLocation=WindowStartupLocation.CenterOwner;}
  var panel=new StackPanel{Margin=new Thickness(24)};
  panel.Children.Add(new ScrollViewer{MaxHeight=470,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)}});
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
  void Add(string label,MessageBoxResult value,bool cancel){var button=new Button{Content=label,MinWidth=82,IsDefault=value==defaultResult||(defaultResult==MessageBoxResult.None&&value==MessageBoxResult.OK),IsCancel=cancel};button.Click+=(_,_)=>{result=value;dialog.DialogResult=true;};actions.Children.Add(button);}
  if(buttons==MessageBoxButton.YesNo){Add("Yes",MessageBoxResult.Yes,false);Add("No",MessageBoxResult.No,true);}
  else Add("OK",MessageBoxResult.OK,true);
  panel.Children.Add(actions);dialog.Content=panel;dialog.ShowDialog();return result;
 }
}
