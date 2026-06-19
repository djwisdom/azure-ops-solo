@echo off
dotnet src\MyCrownJewelApp.Pfpad\bin\Debug\net10.0-windows\MyCrownJewelApp.Pfpad.dll
if errorlevel 1 (
    echo Error level: %errorlevel%
    pause
)
