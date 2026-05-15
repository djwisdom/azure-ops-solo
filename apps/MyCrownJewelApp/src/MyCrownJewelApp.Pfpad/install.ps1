param(
    [switch]$AllUsers,
    [switch]$Uninstall
)

$AppName = "MyCrownJewelApp.Pfpad"
$Version = "1.0.33.0"
$SourceDir = "$PSScriptRoot\bin\Release\net8.0-windows\win-x64\publish"

if ($AllUsers) {
    $InstallDir = "$env:ProgramFiles\$AppName"
    $RegistryRoot = "HKLM:\Software\Classes"
} else {
    $InstallDir = "$env:LOCALAPPDATA\$AppName"
    $RegistryRoot = "HKCU:\Software\Classes"
}

function Install-App {
    Write-Host "Installing $AppName version $Version..."

    # Create install directory
    if (!(Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir | Out-Null
    }

    # Copy files
    Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force

    # Create ProgID
    $progIdPath = "$RegistryRoot\$AppName"
    New-Item -Path $progIdPath -Force | Out-Null
    Set-ItemProperty -Path $progIdPath -Name "(Default)" -Value "$AppName Document"

    $iconPath = "$RegistryRoot\$AppName\DefaultIcon"
    New-Item -Path $iconPath -Force | Out-Null
    Set-ItemProperty -Path $iconPath -Name "(Default)" -Value "$InstallDir\$AppName.exe,0"

    $commandPath = "$RegistryRoot\$AppName\shell\open\command"
    New-Item -Path $commandPath -Force | Out-Null
    Set-ItemProperty -Path $commandPath -Name "(Default)" -Value "`"$InstallDir\$AppName.exe`" `"%1`""

    # Associate .txt files
    $txtAssocPath = "$RegistryRoot\.txt"
    if (!(Test-Path $txtAssocPath)) {
        New-Item -Path $txtAssocPath -Force | Out-Null
    }
    Set-ItemProperty -Path $txtAssocPath -Name "(Default)" -Value $AppName

    # Add to OpenWithList
    $openWithPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\OpenWithList"
    if (!(Test-Path $openWithPath)) {
        New-Item -Path $openWithPath -Force | Out-Null
    }
    Set-ItemProperty -Path $openWithPath -Name "$AppName.exe" -Value ""

    Write-Host "Installation completed successfully."
}

function Uninstall-App {
    Write-Host "Uninstalling $AppName..."

    # Remove files
    if (Test-Path $InstallDir) {
        Remove-Item -Path $InstallDir -Recurse -Force
    }

    # Remove registry entries
    $pathsToRemove = @(
        "$RegistryRoot\$AppName",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\OpenWithList\$AppName.exe"
    )

    foreach ($path in $pathsToRemove) {
        if (Test-Path $path) {
            Remove-Item -Path $path -Recurse -Force
        }
    }

    # Reset .txt association if it was ours
    $txtAssocPath = "$RegistryRoot\.txt"
    if ((Get-ItemProperty -Path $txtAssocPath -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)" -eq $AppName) {
        Remove-ItemProperty -Path $txtAssocPath -Name "(Default)" -ErrorAction SilentlyContinue
    }

    Write-Host "Uninstallation completed successfully."
}

if ($Uninstall) {
    Uninstall-App
} else {
    Install-App
}