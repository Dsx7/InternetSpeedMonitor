@echo off
if exist "%~dp0publish\NetPulseTaskbar.exe" (
    start "" "%~dp0publish\NetPulseTaskbar.exe"
) else (
    echo [ERROR] %~dp0publish\NetPulseTaskbar.exe not found!
    echo Please run build.bat first.
    pause
)
