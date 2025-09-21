using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace KimiAppNative
{
    public partial class App : Application
    {
        // Hotkey IDs for different AI assistants
        private const int KIMI_HOTKEY_ID = 9000;
        private const int CURSOR_HOTKEY_ID = 9001;
        private const int CHATGPT_HOTKEY_ID = 9002;
        private const int CHAT_HOTKEY_ID = 9003;
        
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int VK_G = 0x47;
        private const int VK_C = 0x43;
        private const int VK_H = 0x48;
        private const int VK_A = 0x41;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _windowHandle;
        private MainWindow _mainWindow;
        private ChatWindow _chatWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Create main window
            _mainWindow = new MainWindow();
            _chatWindow = new ChatWindow();
            _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
            
            // Register global hotkeys
            RegisterHotKey(_windowHandle, KIMI_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_G);     // Ctrl+Shift+G for Kimi
            RegisterHotKey(_windowHandle, CURSOR_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_C);   // Ctrl+Shift+C for Cursor
            RegisterHotKey(_windowHandle, CHATGPT_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_H);  // Ctrl+Shift+H for ChatGPT
            RegisterHotKey(_windowHandle, CHAT_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_A);     // Ctrl+Shift+A for Chat
            
            // Add message filter for hotkeys
            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
            
            // Show window initially hidden
            _mainWindow.Hide();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Unregister hotkeys
            UnregisterHotKey(_windowHandle, KIMI_HOTKEY_ID);
            UnregisterHotKey(_windowHandle, CURSOR_HOTKEY_ID);
            UnregisterHotKey(_windowHandle, CHATGPT_HOTKEY_ID);
            UnregisterHotKey(_windowHandle, CHAT_HOTKEY_ID);
            base.OnExit(e);
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == 0x0312) // WM_HOTKEY
            {
                int hotkeyId = msg.wParam.ToInt32();
                
                switch (hotkeyId)
                {
                    case KIMI_HOTKEY_ID:
                        _mainWindow.ShowAssistant("Kimi");
                        break;
                    case CURSOR_HOTKEY_ID:
                        _mainWindow.ShowAssistant("Cursor");
                        break;
                    case CHATGPT_HOTKEY_ID:
                        _mainWindow.ShowAssistant("ChatGPT");
                        break;
                    case CHAT_HOTKEY_ID:
                        ShowChatWindow();
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
    }
}
