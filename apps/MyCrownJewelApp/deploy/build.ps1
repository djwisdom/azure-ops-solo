<#
.SYNOPSIS
    Builds the Personal Flip Pad application and packages it into an Inno Setup installer.

.DESCRIPTION
    This script:
    1. Restores NuGet packages
    2. Builds the project in Release mode
    3. Publishes as a self-contained single-file executable for win-x64
    4. Compiles the Inno Setup installer (.exe) into the deploy\ directory

.PARAMETER NoBuild
    Skip the dotnet publish step and use existing files in deploy\app\

.PARAMETER Version
    Set the version number (default: 1.0.0.0)

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Version 1.2.3.0
    .\build.ps1 -NoBuild
#>

param(
    [switch]$NoBuild,
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$deployDir = Join-Path $rootDir "deploy"
$appDir = Join-Path $deployDir "app"
$netcoreDbgDir = Join-Path $deployDir "netcoredbg"
$projectFile = Join-Path $rootDir "src\MyCrownJewelApp.Pfpad\MyCrownJewelApp.Pfpad.csproj"
$issFile = Join-Path $deployDir "setup.iss"

# Ensure deploy directory exists
New-Item -ItemType Directory -Force -Path $deployDir | Out-Null

if (-not $NoBuild) {
    Write-Host "=== Restoring packages ===" -ForegroundColor Cyan
    dotnet restore $projectFile

    Write-Host "=== Publishing self-contained Release build ===" -ForegroundColor Cyan
    dotnet publish $projectFile `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:Version=$Version `
        -p:ApplicationVersion=$Version `
        -o $appDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Publish failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Publish succeeded." -ForegroundColor Green
} else {
    Write-Host "=== Skipping build (-NoBuild) ===" -ForegroundColor Yellow
}

# Download netcoredbg for the debugger if not already present
$netcoreDbgExe = Join-Path $netcoreDbgDir "netcoredbg.exe"
if (-not (Test-Path $netcoreDbgExe)) {
    Write-Host "=== Downloading netcoredbg ===" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $netcoreDbgDir | Out-Null
    $netcoredbgZipUrl = "https://github.com/Samsung/netcoredbg/releases/latest/download/netcoredbg-win64.zip"
    $zipPath = Join-Path $env:TEMP "netcoredbg-win64.zip"
    try {
        Invoke-WebRequest -Uri $netcoredbgZipUrl -OutFile $zipPath -UseBasicParsing
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        foreach ($entry in $zip.Entries) {
            $targetPath = Join-Path $netcoreDbgDir $entry.FullName
            if ($entry.FullName.EndsWith('/')) {
                New-Item -ItemType Directory -Force -Path $targetPath | Out-Null
            } else {
                $dir = Split-Path $targetPath -Parent
                if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $targetPath, $true)
            }
        }
        $zip.Dispose()
        # Flatten if zip contained a root folder (e.g. netcoredbg/)
        $subDir = Join-Path $netcoreDbgDir "netcoredbg"
        if (Test-Path $subDir) {
            Get-ChildItem -Path $subDir -Recurse | Move-Item -Destination $netcoreDbgDir -Force
            Remove-Item $subDir -Recurse -Force
        }
        Remove-Item $zipPath -Force
        Write-Host "netcoredbg downloaded to $netcoreDbgDir" -ForegroundColor Green
    } catch {
        Write-Host "WARNING: Could not download netcoredbg: $_" -ForegroundColor Yellow
        Write-Host "The debugger feature will not be available out of the box." -ForegroundColor Yellow
        Write-Host "Users can install it manually: https://github.com/Samsung/netcoredbg" -ForegroundColor Yellow
    }
} else {
    Write-Host "=== netcoredbg already present ===" -ForegroundColor Cyan
}

# Update version in .iss file
(Get-Content $issFile) -replace '#define MyAppVersion "[\d.]+"', "#define MyAppVersion `"$Version`"" | Set-Content $issFile

# Check for Inno Setup
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = "C:\Program Files\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $iscc)) {
    Write-Host "Inno Setup not found at $iscc" -ForegroundColor Red
    Write-Host "Download and install from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Then run this script again." -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Compiling Inno Setup installer ===" -ForegroundColor Cyan
Push-Location $deployDir
try {
    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Installer compilation failed!" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Green
$installer = Get-ChildItem "$deployDir\PersonalFlipPad-Setup-$Version.exe"
if ($installer) {
    Write-Host "Installer: $($installer.FullName)" -ForegroundColor Green
    Write-Host "Size: $('{0:N0} KB' -f ($installer.Length / 1KB))" -ForegroundColor Green
}

Write-Host ""
Write-Host "Install options:" -ForegroundColor Cyan
Write-Host "  Per-user:   $($installer.Name) /CURRENTUSER" -ForegroundColor Gray
Write-Host "  All-users:  $($installer.Name) /ALLUSERS" -ForegroundColor Gray
Write-Host "  Silent:     $($installer.Name) /VERYSILENT /CURRENTUSER" -ForegroundColor Gray
