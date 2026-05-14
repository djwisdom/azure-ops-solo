param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [string]$OutputDir = "$PSScriptRoot\installer",

    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# Clean output directory if requested
if ($Clean -and (Test-Path $OutputDir)) {
    Write-Host "Cleaning output directory: $OutputDir"
    Remove-Item -Recurse -Force $OutputDir
}

# Create output directory
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Publish the application as self-contained
Write-Host "Publishing application..."
$publishDir = "$OutputDir\publish"
dotnet publish "$PSScriptRoot\MyCrownJewelApp.Pfpad.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish application"
    exit 1
}

# Copy published files to installer directory
Write-Host "Preparing installer files..."
$installerFilesDir = "$OutputDir\InstallerFiles"
if (!(Test-Path $installerFilesDir)) {
    New-Item -ItemType Directory -Path $installerFilesDir | Out-Null
}

# Copy main executable and dependencies
Copy-Item "$publishDir\*" -Destination $installerFilesDir -Recurse -Force

# Generate unique GUIDs for the installer
$appId = [guid]::NewGuid().ToString().ToUpper()
$clsid = [guid]::NewGuid().ToString().ToUpper()

# Update the Inno Setup script with the generated GUIDs
$issContent = Get-Content "$PSScriptRoot\installer.iss" -Raw
$issContent = $issContent -replace "YOUR-APP-ID-HERE", $appId.ToLower()
Set-Content "$OutputDir\installer.iss" $issContent

# Build the installer using Inno Setup
Write-Host "Building installer..."
$innoSetupPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (!(Test-Path $innoSetupPath)) {
    $innoSetupPath = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
}
if (!(Test-Path $innoSetupPath)) {
    Write-Error "Inno Setup 6 not found. Please install Inno Setup from https://jrsoftware.org/isinfo.php"
    exit 1
}

Push-Location $PSScriptRoot
try {
    & $innoSetupPath "$OutputDir\installer.iss"

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer built successfully!"
        Write-Host "Output: $OutputDir\PersonalFlipPad-1.0.31.0.exe"
        Write-Host ""
        Write-Host "To install for current user: Run the installer as current user"
        Write-Host "To install for all users: Run the installer as administrator"
        Write-Host ""
        Write-Host "The installer supports:"
        Write-Host "- Per-user or per-machine installation"
        Write-Host "- File associations for text/code files"
        Write-Host "- 'Open with' context menu integration"
        Write-Host "- Start menu and desktop shortcuts"
    } else {
        Write-Error "Failed to build installer"
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Build completed successfully!"
Write-Host "Installer location: $OutputDir\PersonalFlipPad.msi"