#nullable enable
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace KimiAppNative
{
    public partial class App : Application
    {
        // Hotkey IDs for different AI assistants
        private const int KIMI_HOTKEY_ID = 9000;
        private const int CURSOR_HOTKEY_ID = 9001;
        private const int CHATGPT_HOTKEY_ID = 9002;
        private const int CHAT_HOTKEY_ID = 9003;
        private const int GUI_CREATOR_HOTKEY_ID = 9004;
        private const int IDE_HOTKEY_ID = 9005;
        
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int VK_NUMPAD1 = 0x61; // Numpad 1
        private const int VK_NUMPAD2 = 0x62; // Numpad 2
        private const int VK_NUMPAD3 = 0x63; // Numpad 3
        private const int VK_NUMPAD4 = 0x64; // Numpad 4
        private const int VK_NUMPAD5 = 0x65; // Numpad 5
        private const int VK_NUMPAD6 = 0x66; // Numpad 6

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetLastError();

        // Use the built-in MSG structure from System.Windows.Interop

        private IntPtr _windowHandle;
        private MainWindow? _mainWindow;
        private ChatWindow? _chatWindow;
        private GuiTemplateCreator? _guiCreator;
        private IDE.IDEMainWindow? _ideWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Create main window
            _mainWindow = new MainWindow();
            _chatWindow = new ChatWindow();
            _guiCreator = new GuiTemplateCreator();
            _ideWindow = new IDE.IDEMainWindow();
            
            // Wait for window to be fully initialized before getting handle
            if (_mainWindow != null)
            {
                _mainWindow.SourceInitialized += MainWindow_SourceInitialized;
                
                // Add message filter for hotkeys
                ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
                
                // Show window initially hidden
                _mainWindow.Hide();
            }
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            // Now we can safely get the window handle
            if (_mainWindow != null)
            {
                _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
            }
            
            // Register global hotkeys with error handling
            RegisterHotkeyWithErrorHandling("Kimi", KIMI_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD1, "Ctrl+Shift+Numpad1");
            RegisterHotkeyWithErrorHandling("Cursor", CURSOR_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD2, "Ctrl+Shift+Numpad2");
            RegisterHotkeyWithErrorHandling("ChatGPT", CHATGPT_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD3, "Ctrl+Shift+Numpad3");
            RegisterHotkeyWithErrorHandling("Chat", CHAT_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD4, "Ctrl+Shift+Numpad4");
            RegisterHotkeyWithErrorHandling("GUI Creator", GUI_CREATOR_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD5, "Ctrl+Shift+Numpad5");
            RegisterHotkeyWithErrorHandling("OhGees IDE", IDE_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD6, "Ctrl+Shift+Numpad6");
        }

        private void RegisterHotkeyWithErrorHandling(string name, int id, int modifiers, int vk, string keyCombo)
        {
            bool success = RegisterHotKey(_windowHandle, id, modifiers, vk);
            if (success)
            {
                System.Diagnostics.Debug.WriteLine($"✅ {name} hotkey ({keyCombo}) registered successfully");
            }
            else
            {
                int error = GetLastError();
                System.Diagnostics.Debug.WriteLine($"❌ Failed to register {name} hotkey ({keyCombo}). Error: {error}");
                
                // Try alternative hotkey combinations if the first one fails
                if (error == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Hotkey {keyCombo} is already registered by another application");
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
                // Unregister hotkeys
                UnregisterHotKey(_windowHandle, KIMI_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, CURSOR_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, CHATGPT_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, CHAT_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, GUI_CREATOR_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, IDE_HOTKEY_ID);
            base.OnExit(e);
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == 0x0312) // WM_HOTKEY
            {
                int hotkeyId = msg.wParam.ToInt32();
                System.Diagnostics.Debug.WriteLine($"🔥 Hotkey pressed! ID: {hotkeyId}");
                
                switch (hotkeyId)
                {
                    case KIMI_HOTKEY_ID:
                        System.Diagnostics.Debug.WriteLine("🎯 Opening Kimi AI");
                        _mainWindow.ShowAssistant("Kimi");
                        break;
                    case CURSOR_HOTKEY_ID:
                        System.Diagnostics.Debug.WriteLine("🎯 Opening Cursor");
                        _mainWindow.ShowAssistant("Cursor");
                        break;
                    case CHATGPT_HOTKEY_ID:
                        System.Diagnostics.Debug.WriteLine("🎯 Opening ChatGPT");
                        _mainWindow.ShowAssistant("ChatGPT");
                        break;
                        case CHAT_HOTKEY_ID:
                            System.Diagnostics.Debug.WriteLine("🎯 Opening Chat Window");
                            ShowChatWindow();
                            break;
                        case GUI_CREATOR_HOTKEY_ID:
                            System.Diagnostics.Debug.WriteLine("🎯 Opening GUI Template Creator");
                            ShowGuiCreator();
                            break;
                        case IDE_HOTKEY_ID:
                            System.Diagnostics.Debug.WriteLine("🎯 Opening OhGees IDE");
                            ShowIDE();
                            break;
                        default:
                            System.Diagnostics.Debug.WriteLine($"⚠️ Unknown hotkey ID: {hotkeyId}");
                            break;
                }
                
                handled = true;
            }
        }

        private void ShowChatWindow()
        {
            if (_chatWindow.Visibility == Visibility.Visible)
            {
                _chatWindow.Hide();
            }
            else
            {
                _chatWindow.Show();
                _chatWindow.Activate();
                _chatWindow.Topmost = true;
                _chatWindow.Topmost = false;
            }
        }

        private void ShowGuiCreator()
        {
            if (_guiCreator != null)
            {
                if (_guiCreator.Visibility == Visibility.Visible)
                {
                    _guiCreator.Hide();
                }
                else
                {
                    _guiCreator.Show();
                    _guiCreator.Activate();
                    _guiCreator.Topmost = true;
                    _guiCreator.Topmost = false;
                }
            }
        }

        private void ShowIDE()
        {
            if (_ideWindow != null)
            {
                if (_ideWindow.Visibility == Visibility.Visible)
                {
                    _ideWindow.Hide();
                }
                else
                {
                    _ideWindow.Show();
                    _ideWindow.Activate();
                    _ideWindow.Topmost = true;
                    _ideWindow.Topmost = false;
                }
            }
        }
    }
}
