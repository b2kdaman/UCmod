param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$managed=Join-Path (Resolve-Path -LiteralPath $GamePath).Path 'uc_Data\Managed'
$outputDir=Join-Path $repoRoot 'build'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$compilerArgs=@('/nologo','/noconfig','/target:library','/nostdlib')
foreach($assembly in @('mscorlib','System','System.Core','UnityEngine')) { $compilerArgs+='/r:'+(Join-Path $managed ($assembly+'.dll')) }
$compilerArgs+='/r:'+(Join-Path $repoRoot 'uc_Data\Managed\0Harmony.dll')
# A unique identity permits live Mono reloads.
$buildPath=Join-Path $outputDir ('ADSFix.'+[Guid]::NewGuid().ToString('N')+'.dll')
$compilerArgs+='/out:'+$buildPath
$compilerArgs+=(Join-Path $PSScriptRoot 'ADSFix.cs')
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" @compilerArgs
if($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath $buildPath -Destination (Join-Path $outputDir 'ADSFix.dll') -Force
Remove-Item -LiteralPath $buildPath
Write-Host 'Compiled into build/. Test changed builds before distributing.'
