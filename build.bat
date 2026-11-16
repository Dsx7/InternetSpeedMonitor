@echo off
setlocal
echo ================================================================
echo  Building NetPulse Taskbar (Production Release)
echo  Author : Al Helal Mohammod Bayijid
echo  Repo   : https://github.com/Dsx7/InternetSpeedMonitor
echo ================================================================

if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
) else (
    where dotnet >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        set "DOTNET_CMD=dotnet"
    ) else (
        echo [ERROR] .NET 8.0 SDK was not found!
        exit /b 1
    )
)

echo.
echo [1/3] Building Solution...
"%DOTNET_CMD%" build "%~dp0NetPulseTaskbar.sln" -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Solution build failed!
    exit /b %ERRORLEVEL%
)

echo.
echo [2/3] Publishing Standalone Self-Contained Binary...
"%DOTNET_CMD%" publish "%~dp0src\NetPulseTaskbar\NetPulseTaskbar.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -p:EnableCompressionInSingleFile=true -p:DebuggerSupport=false -p:DebugSymbols=false -p:DebugType=none -o "%~dp0publish"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Standalone publish failed!
    exit /b %ERRORLEVEL%
)

echo.
echo [3/3] Publishing Lightweight Binary (Framework-Dependent)...
"%DOTNET_CMD%" publish "%~dp0src\NetPulseTaskbar\NetPulseTaskbar.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebuggerSupport=false -p:DebugSymbols=false -p:DebugType=none -o "%~dp0publish_temp"
if exist "%~dp0publish_temp\NetPulseTaskbar.exe" (
    copy /Y "%~dp0publish_temp\NetPulseTaskbar.exe" "%~dp0publish\NetPulseTaskbar-Lightweight.exe" >nul
    rmdir /S /Q "%~dp0publish_temp" >nul 2>&1
)

echo.
echo ================================================================
echo  BUILD COMPLETE!
echo  Standalone   : %~dp0publish\NetPulseTaskbar.exe
echo  Lightweight  : %~dp0publish\NetPulseTaskbar-Lightweight.exe
echo ================================================================
endlocal
