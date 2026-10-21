param([string]$Dotnet)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build-environment.ps1')
$dotnetPath=Initialize-SmartFilesBuild -Dotnet $Dotnet
if(-not (Test-Path -LiteralPath 'D:\')){throw 'Tests require a writable D: drive'}
$artifacts='D:\SmartFilesApp\DevArtifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
& $dotnetPath run --project (Join-Path $PSScriptRoot 'src\SmartFiles.Tests\SmartFiles.Tests.csproj') -c Release
$testExit=$LASTEXITCODE
[ordered]@{checkedUtc=[DateTime]::UtcNow.ToString('o');exitCode=$testExit;passed=($testExit -eq 0)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'github-source-test-attempt.json') -Encoding utf8
if($testExit -ne 0){throw 'Safety tests failed'}
