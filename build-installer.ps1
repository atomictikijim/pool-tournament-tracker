<#
.SYNOPSIS
    Publishes PoolTournamentManager as a self-contained single-file exe and builds the
    Windows installer (Setup.exe) with Inno Setup.

.DESCRIPTION
    1. dotnet publish (Release, win-x64, self-contained, single-file) to publish/win-x64
    2. Locates ISCC.exe (Inno Setup Compiler) and compiles installer/PoolTournamentManager.iss
    3. Output: installer/output/PoolTournamentManager-Setup-v<version>.exe

    Requires Inno Setup 6 (install once with:
    winget install --id JRSoftware.InnoSetup -e)
#>

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$appProject = Join-Path $repoRoot "src\PoolTournamentManager.App\PoolTournamentManager.App.csproj"
$publishDir = Join-Path $repoRoot "publish\win-x64"
$issScript = Join-Path $repoRoot "installer\PoolTournamentManager.iss"

Write-Host "==> Publishing self-contained single-file build to $publishDir" -ForegroundColor Cyan
dotnet publish $appProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "==> Locating Inno Setup Compiler (ISCC.exe)" -ForegroundColor Cyan
$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw "Could not find ISCC.exe (Inno Setup Compiler). Install it with:`n" +
          "  winget install --id JRSoftware.InnoSetup -e`nthen re-run this script."
}
Write-Host "    Using $iscc"

Write-Host "==> Compiling installer" -ForegroundColor Cyan
& $iscc $issScript

if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}

Write-Host "==> Done. Installer is in installer\output\" -ForegroundColor Green
Get-ChildItem (Join-Path $repoRoot "installer\output") -Filter "*.exe" |
    Select-Object Name, @{Name="SizeMB";Expression={[math]::Round($_.Length / 1MB, 1)}}
