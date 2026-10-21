function Initialize-SmartFilesBuild {
 param([string]$Dotnet)
 $ErrorActionPreference='Stop'
 if(-not $Dotnet){
  $bundled='D:\SmartFilesApp\DevTools\dotnet\dotnet.exe'
  if(Test-Path -LiteralPath $bundled){$Dotnet=$bundled}
  else {$Dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
 }
 $buildHome=Join-Path $PSScriptRoot '.build'
 $tempRoot=Join-Path $buildHome 'temp'
 New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
 $env:DOTNET_CLI_HOME=Join-Path $buildHome 'dotnet-home'
 if(-not $env:NUGET_PACKAGES){$env:NUGET_PACKAGES=Join-Path $buildHome 'packages'}
 $env:NUGET_HTTP_CACHE_PATH=Join-Path $buildHome 'nuget-http'
 $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
 $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
 $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
 $env:TEMP=$tempRoot
 $env:TMP=$tempRoot
 return $Dotnet
}
