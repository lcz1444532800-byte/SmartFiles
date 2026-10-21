using System.Runtime.InteropServices;
using System.Text;
namespace SmartFiles.Core;
internal static class MsiMetadata {
 [DllImport("msi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern uint MsiOpenDatabaseW(string path,nint mode,out uint database);
 [DllImport("msi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern uint MsiDatabaseOpenViewW(uint database,string query,out uint view);
 [DllImport("msi.dll",ExactSpelling=true)]static extern uint MsiViewExecute(uint view,uint record);
 [DllImport("msi.dll",ExactSpelling=true)]static extern uint MsiViewFetch(uint view,out uint record);
 [DllImport("msi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern uint MsiRecordGetStringW(uint record,uint field,StringBuilder value,ref uint length);
 [DllImport("msi.dll",ExactSpelling=true)]static extern uint MsiCloseHandle(uint handle);
 public static string Read(string path){
  // MSIDBOPEN_READONLY is zero: this only queries three properties, never installs or executes the package.
  if(MsiOpenDatabaseW(path,0,out var database)!=0)return "";
  try{var result=new StringBuilder();foreach(var name in new[]{"ProductName","Manufacturer","ProductVersion"}){
   if(MsiDatabaseOpenViewW(database,$"SELECT `Value` FROM `Property` WHERE `Property` = '{name}'",out var view)!=0)continue;
   try{if(MsiViewExecute(view,0)!=0||MsiViewFetch(view,out var record)!=0)continue;
    try{uint length=383;var value=new StringBuilder(384);var status=MsiRecordGetStringW(record,1,value,ref length);if(status==0||status==234)result.Append(name).Append(':').Append(value).Append(' ');}
    finally{MsiCloseHandle(record);}
   }finally{MsiCloseHandle(view);}
  }return result.ToString();}finally{MsiCloseHandle(database);}
 }
}
