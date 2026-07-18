@echo off
echo Building ENI Auto Updater...
csc /out:auto_updater.exe /target:exe /reference:System.Net.Http.dll Program.cs
echo.
if exist auto_updater.exe (
    echo Build successful! auto_updater.exe created.
) else (
    echo Build failed!
)
pause
