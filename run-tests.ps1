param([string]$Suite = '')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$outDir = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','Microsoft.CSharp.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationFramework.dll','PresentationCore.dll','UIAutomationClient.dll','UIAutomationTypes.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_)) }
$sources = @()
if (Test-Path (Join-Path $PSScriptRoot 'src')) { $sources += Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | ForEach-Object FullName }
$sources += Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File | ForEach-Object FullName
$binary = Join-Path $outDir 'OverlayTests.exe'
& $compiler /nologo /langversion:5 /target:exe /main:CodexUsageOverlay.Tests.TestRunner ('/out:' + $binary) @references @sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $binary $Suite
exit $LASTEXITCODE
