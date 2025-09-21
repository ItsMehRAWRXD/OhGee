# OhGee Hotkey Troubleshooting Guide

## 🔥 **Current Hotkeys:**
- **Ctrl+Shift+Numpad1** - Open Kimi AI
- **Ctrl+Shift+Numpad2** - Open Cursor  
- **Ctrl+Shift+Numpad3** - Open ChatGPT
- **Ctrl+Shift+Numpad4** - Open Native Chat Assistant

## 🚨 **If Hotkeys Don't Work:**

### 1. **Check if Application is Running**
- Look for the OhGee icon in your system tray (bottom-right corner)
- If not visible, run the application again

### 2. **Check for Conflicting Applications**
- Another application might be using the same hotkeys
- Common conflicts: Discord, Steam, other AI tools
- Try closing other applications and test again

### 3. **Run as Administrator**
- Right-click on `KimiAppNative.exe` 
- Select "Run as administrator"
- This gives the app permission to register global hotkeys

### 4. **Check Debug Output**
- Run `run-debug.bat` to see debug messages
- Look for hotkey registration status messages
- Check if any hotkeys failed to register

### 5. **Alternative Hotkey Combinations**
If the default hotkeys don't work, you can modify them in `App.xaml.cs`:
- Change the virtual key codes (VK_G, VK_C, VK_H, VK_A)
- Change the modifiers (MOD_CONTROL, MOD_SHIFT)

### 6. **System Tray Access**
- Right-click the OhGee icon in system tray
- Select the AI assistant you want to open
- This bypasses hotkey issues

## 🔧 **Debug Information:**
- Hotkey registration happens when the app starts
- Debug messages show in console when running `run-debug.bat`
- Error codes help identify specific issues

## 📝 **Common Error Codes:**
- **1409** - Hotkey already registered by another app
- **5** - Access denied (try running as admin)
- **87** - Invalid parameter

## ✅ **Quick Test:**
1. Run the application or install as service
2. Press **Ctrl+Shift+Numpad4** (should open native chat)
3. If that works, try the other hotkeys
4. If none work, check system tray menu

## 🔧 **Service Mode:**
- Run `install-service.bat` as Administrator to install as Windows Service
- Service runs in background and appears in Task Manager
- Hotkeys work system-wide even when no visible windows
- Use `uninstall-service.bat` to remove the service

## 🆘 **Still Not Working?**
- Check Windows Event Viewer for errors
- Try restarting the application
- Reboot your computer
- Check if Windows Defender is blocking the app
