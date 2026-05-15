@echo off
echo MyCrownJewelApp Pfpad Installer v1.0.33.0
echo.
echo Choose installation type:
echo 1. Install for current user
echo 2. Install for all users (requires admin)
echo 3. Uninstall
echo.

set /p choice="Enter choice (1-3): "

if "%choice%"=="1" (
    powershell -ExecutionPolicy Bypass -File "%~dp0install.ps1"
) else if "%choice%"=="2" (
    powershell -ExecutionPolicy Bypass -File "%~dp0install.ps1" -AllUsers
) else if "%choice%"=="3" (
    powershell -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall
) else (
    echo Invalid choice.
)

pause