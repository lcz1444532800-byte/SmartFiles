param([switch]$Publish,[string]$Dotnet)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build-environment.ps1')
$dotnetPath=Initialize-SmartFilesBuild -Dotnet $Dotnet
foreach($name in @('SmartFiles','SmartFiles.AIWorker','SmartFiles.Watcher','SmartFiles.Worker')){
 $project=Join-Path $PSScriptRoot "src\$name\$name.csproj"
 if($Publish){
  $relative=switch($name){
   'SmartFiles.AIWorker' {'Release\AIWorker'}
   'SmartFiles.Watcher' {'Release\Watcher'}
   'SmartFiles.Worker' {'Release\Worker'}
   default {'Release'}
  }
  & $dotnetPath publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $PSScriptRoot $relative) --nologo
 } else { & $dotnetPath build $project -c Release --nologo }
 if($LASTEXITCODE -ne 0){throw "Build failed: $name"}
}
