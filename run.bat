@echo off
echo Starting AI Assistant Hub...

REM Check if .NET 8.0 is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET 8.0 is not installed or not in PATH
    echo Please install .NET 8.0 from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

REM Check if the application is already built
if not exist "bin\Release\net8.0-windows\KimiAppNative.exe" (
    echo Application not built. Building now...
    call build.bat
    if %errorlevel% neq 0 (
        echo Build failed!
        pause
        exit /b 1
    )
)

REM Run the application
echo Starting AI Assistant Hub...
start "" "bin\Release\net8.0-windows\KimiAppNative.exe"

echo Application started successfully!
echo The application is now running in the system tray.
echo Use the following hotkeys:
echo   Ctrl+Shift+G - Open Kimi AI
echo   Ctrl+Shift+C - Open Cursor
echo   Ctrl+Shift+H - Open ChatGPT
echo.
echo Right-click the system tray icon to access the menu.
