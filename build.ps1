param()
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$outDir = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','Microsoft.CSharp.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationFramework.dll','PresentationCore.dll','UIAutomationClient.dll','UIAutomationTypes.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_)) }
$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | ForEach-Object FullName
& $compiler /nologo /langversion:5 /target:winexe /platform:x64 /optimize+ /main:CodexUsageOverlay.Program ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/out:' + (Join-Path $outDir 'CodexUsageOverlay.exe')) @references @sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$config = '<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup><runtime><AppContextSwitchOverrides value="Switch.System.Windows.DoNotScaleForDpiChanges=false" /></runtime></configuration>'
[System.IO.File]::WriteAllText((Join-Path $outDir 'CodexUsageOverlay.exe.config'), $config, [System.Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $outDir 'CodexUsageOverlay.exe')
