param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$managed=Join-Path (Resolve-Path -LiteralPath $GamePath).Path 'uc_Data\Managed'
$outputDir=Join-Path $repoRoot 'build'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$compilerArgs=@('/nologo','/noconfig','/target:library','/nostdlib')
foreach($assembly in @('mscorlib','System','System.Core','UnityEngine','UnityEngine.UI')) { $compilerArgs+='/r:'+(Join-Path $managed ($assembly+'.dll')) }
$buildPath=Join-Path $outputDir 'CameraFix.dll'
$compilerArgs+='/out:'+$buildPath
$compilerArgs+=(Join-Path $PSScriptRoot 'CameraFix.cs')
$compilerArgs+=(Join-Path $PSScriptRoot 'RuntimeInspect.cs')
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" @compilerArgs
if($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host 'Compiled into build/. Test changed builds before distributing.'
