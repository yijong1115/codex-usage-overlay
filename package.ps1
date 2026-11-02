param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'

# Build and test before assembling a new, empty directory from known files.
& powershell -NoProfile -ExecutionPolicy RemoteSigned -File (Join-Path $PSScriptRoot 'run-tests.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; no package created.' }
& powershell -NoProfile -ExecutionPolicy RemoteSigned -File (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no package created.' }

$artifactRoot = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$zipPath = Join-Path $artifactRoot ('CodexUsageOverlay-' + $Version + '-windows-x64.zip')
if (Test-Path -LiteralPath $zipPath) { throw 'That package already exists. Use a new version or move the existing package first.' }
$stagingRoot = Join-Path $artifactRoot ('stage-' + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stagingRoot 'CodexUsageOverlay'
$files = @('out\CodexUsageOverlay.exe', 'out\CodexUsageOverlay.exe.config', 'assets\sleeping-cat.png', 'README.md', 'PRIVACY.md', 'LICENSE', 'Start.cmd', 'docs\TESTING.md', 'docs\preview.png')
foreach ($relativePath in $files) {
    $sourcePath = Join-Path $PSScriptRoot $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw ('Missing runtime file: ' + $relativePath) }
    $targetPath = Join-Path $packageRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path $targetPath -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath
}
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
$checksum = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zipPath + '.sha256'), $checksum + '  ' + [IO.Path]::GetFileName($zipPath) + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
Write-Output ('Package: ' + $zipPath)
Write-Output ('SHA256: ' + $checksum)

