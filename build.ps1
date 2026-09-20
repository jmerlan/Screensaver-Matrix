# Builds the screensaver into dist\Matrix.scr.
#   .\build.ps1             screensaver only
#   .\build.ps1 -Installer  also builds dist\MatrixScreensaverSetup-<version>.exe (needs Inno Setup 6)
param([switch]$Installer)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build MatrixScreensaver.csproj -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

New-Item -ItemType Directory -Force dist | Out-Null
Copy-Item bin\Release\net48\Matrix.exe dist\Matrix.scr -Force
Write-Host "Built $(Resolve-Path dist\Matrix.scr)"

if ($Installer) {
    $iscc = @(
        (Get-Command iscc -ErrorAction SilentlyContinue).Source,
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

    $version = ([xml](Get-Content MatrixScreensaver.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    & $iscc /Q "/DAppVersion=$version" installer\Matrix.iss
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
    Get-ChildItem dist\MatrixScreensaverSetup-*.exe | Sort-Object LastWriteTime | Select-Object -Last 1 |
        ForEach-Object { Write-Host "Built $($_.FullName)" }
}
