# Personal Flip Pad Installer

This directory contains the installer files for Personal Flip Pad version 1.0.46.0.

## Building the Installer

### Prerequisites

1. **.NET 10 SDK** - For publishing the application
2. **Inno Setup 6** - For building the installer
   - Download from: https://jrsoftware.org/isinfo.php

### Build Steps

1. **Publish the application:**
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. **Run the installer build script:**
   ```powershell
   .\build-installer.ps1
   ```

   Or with custom options:
   ```powershell
   .\build-installer.ps1 -Configuration Release -OutputDir ".\installer" -Clean
   ```

3. **The installer will be created as:**
   `installer\pfpad-Setup-1.0.46.0.exe`

## Installation Options

### Per-User Installation (Current)
- Run the installer as a regular user
- Installs to `%LOCALAPPDATA%\Personal Flip Pad`
- Available only to the current user
- No admin rights required

### Installation Scope
- The current installer is per-user only
- It does not install into `Program Files`
- Each user gets a separate install under `%LOCALAPPDATA%\Personal Flip Pad`

## Features

The installer provides:

- **File Associations:** Automatically associates common text/code file types (.txt, .cs, .js, .py, .cpp, .h, .json, .xml, .md)
- **Open With Menu:** Adds Personal Flip Pad to the "Open with" context menu in File Explorer
- **Shortcuts:** Creates Start Menu and optional Desktop shortcuts
- **Uninstaller:** Properly removes all associations and shortcuts on uninstall

## File Associations

During installation, you can choose which file types to associate with Personal Flip Pad:

- Text files (.txt)
- C# files (.cs)
- JavaScript files (.js)
- Python files (.py)
- C++ files (.cpp, .h)
- JSON files (.json)
- XML files (.xml)
- Markdown files (.md)

## Context Menu Integration

After installation, Personal Flip Pad will appear in:

1. **File Context Menu:** Right-click any file → "Open with" → "Personal Flip Pad"
2. **Folder Context Menu:** Right-click folders to open them in the workspace
3. **Desktop Context Menu:** Right-click empty desktop areas

## Troubleshooting

### Installer won't start
- Ensure Inno Setup 6 is installed
- Check that .NET 10 SDK is available
- Run the build script from an elevated PowerShell prompt

### File associations don't work
- Re-run the per-user installer so the current user's associations are refreshed
- Check that the file extensions are properly associated in Windows Settings

### Context menu doesn't appear
- Restart File Explorer (Ctrl+Shift+Esc → Processes → explorer.exe → End task → File → Run new task → explorer.exe)
- Or restart Windows

## Version History

- **1.0.46.0** - Installer/output naming aligned to `pfpad-Setup-*`; per-user install path standardized to `%LOCALAPPDATA%\Personal Flip Pad`
- **1.0.30.0** - Multi-layered vim undo/redo, menu reorganization, installer creation
- **1.0.29.0** - Previous version with UI improvements

## Support

For issues or questions:
- GitHub Issues: https://github.com/djwisdom/azure-ops-solo/issues
- Documentation: https://github.com/djwisdom/azure-ops-solo