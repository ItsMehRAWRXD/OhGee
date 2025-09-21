# AI Assistant Hub - Installation Guide

## Quick Start

### Option 1: Run from Source (Recommended)
1. Ensure you have .NET 8.0 SDK installed
2. Open Command Prompt or PowerShell in the project directory
3. Run: `dotnet run`

### Option 2: Build and Run
1. Run the build script: `build.bat` or `build.ps1`
2. Navigate to the `publish` folder
3. Run: `KimiAppNative.exe`

### Option 3: Use the Launcher
1. Run: `run.bat`
2. The application will build automatically if needed

## Features

### Global Hotkeys
- **Ctrl+Shift+G**: Open Kimi AI
- **Ctrl+Shift+C**: Open Cursor
- **Ctrl+Shift+H**: Open ChatGPT

### System Tray
- **Left-click**: Toggle window visibility
- **Double-click**: Open Kimi AI
- **Right-click**: Access context menu

### Window Controls
- **Minimize**: Hides to system tray
- **Close**: Hides window (continues running in tray)
- **Drag**: Move window by dragging title bar

## Requirements

- Windows 10/11
- .NET 8.0 Runtime (for running) or SDK (for building)
- WebView2 Runtime (usually pre-installed)

## Troubleshooting

### Build Issues
- Ensure .NET 8.0 SDK is installed
- Run `dotnet --version` to verify
- Clean and rebuild: `dotnet clean && dotnet build`

### Runtime Issues
- Install WebView2 Runtime if missing
- Run as administrator if hotkeys don't work
- Check Windows Defender/antivirus settings

### Hotkey Conflicts
- Close other applications using the same hotkeys
- Restart the application
- Run as administrator

## File Structure

```
KimiAppNative/
├── App.xaml                 # Application definition
├── App.xaml.cs             # Hotkey handling and app logic
├── MainWindow.xaml         # Main window UI
├── MainWindow.xaml.cs      # Window logic and WebView
├── SystemTrayManager.cs    # System tray functionality
├── KimiAppNative.csproj    # Project configuration
├── build.bat              # Windows build script
├── build.ps1              # PowerShell build script
├── run.bat                # Quick launcher
└── README.md              # Documentation
```

## Development

### Building
```bash
dotnet restore
dotnet build --configuration Release
dotnet publish --configuration Release --runtime win-x64 --self-contained true
```

### Running
```bash
dotnet run
```

## Support

For issues or questions, please check the README.md file or create an issue in the repository.
