using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace KimiAppNative
{
    public class OhGeeBackgroundService : BackgroundService
    {
        private readonly ILogger<OhGeeBackgroundService> _logger;
        private MainWindow _mainWindow;
        private ChatWindow _chatWindow;
        private IntPtr _windowHandle;
        private SystemTrayManager _trayManager;

        // Hotkey IDs for different AI assistants
        private const int KIMI_HOTKEY_ID = 9000;
        private const int CURSOR_HOTKEY_ID = 9001;
        private const int CHATGPT_HOTKEY_ID = 9002;
        private const int CHAT_HOTKEY_ID = 9003;
        
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int VK_NUMPAD1 = 0x61; // Numpad 1
        private const int VK_NUMPAD2 = 0x62; // Numpad 2
        private const int VK_NUMPAD3 = 0x63; // Numpad 3
        private const int VK_NUMPAD4 = 0x64; // Numpad 4

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetLastError();

        // MSG structure for Windows messages
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        public OhGeeBackgroundService(ILogger<OhGeeBackgroundService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OhGee Background Service starting...");

            // Create a new STA thread for WPF
            var thread = new Thread(() =>
            {
                try
                {
                    // Initialize WPF application
                    var app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    // Create windows
                    _mainWindow = new MainWindow();
                    _chatWindow = new ChatWindow();
                    _trayManager = new SystemTrayManager(_mainWindow);

                    // Wait for window to be fully initialized
                    _mainWindow.SourceInitialized += MainWindow_SourceInitialized;

                    // Add message filter for hotkeys
                    ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;

                    // Hide windows initially
                    _mainWindow.Hide();

                    _logger.LogInformation("OhGee windows initialized. Hotkeys registered.");

                    // Run the WPF message loop
                    app.Run();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in WPF thread");
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait for cancellation
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("OhGee Background Service stopping...");
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            // Now we can safely get the window handle
            _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
            
            // Register global hotkeys with error handling
            RegisterHotkeyWithErrorHandling("Kimi", KIMI_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD1, "Ctrl+Shift+Numpad1");
            RegisterHotkeyWithErrorHandling("Cursor", CURSOR_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD2, "Ctrl+Shift+Numpad2");
            RegisterHotkeyWithErrorHandling("ChatGPT", CHATGPT_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD3, "Ctrl+Shift+Numpad3");
            RegisterHotkeyWithErrorHandling("Chat", CHAT_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NUMPAD4, "Ctrl+Shift+Numpad4");
        }

        private void RegisterHotkeyWithErrorHandling(string name, int id, int modifiers, int vk, string keyCombo)
        {
            bool success = RegisterHotKey(_windowHandle, id, modifiers, vk);
            if (success)
            {
                _logger.LogInformation($"✅ {name} hotkey ({keyCombo}) registered successfully");
            }
            else
            {
                int error = GetLastError();
                _logger.LogWarning($"❌ Failed to register {name} hotkey ({keyCombo}). Error: {error}");
                
                if (error == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
                {
                    _logger.LogWarning($"⚠️ Hotkey {keyCombo} is already registered by another application");
                }
            }
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == 0x0312) // WM_HOTKEY
            {
                int hotkeyId = msg.wParam.ToInt32();
                _logger.LogInformation($"🔥 Hotkey pressed! ID: {hotkeyId}");
                
                switch (hotkeyId)
                {
                    case KIMI_HOTKEY_ID:
                        _logger.LogInformation("🎯 Opening Kimi AI");
                        _mainWindow?.ShowAssistant("Kimi");
                        break;
                    case CURSOR_HOTKEY_ID:
                        _logger.LogInformation("🎯 Opening Cursor");
                        _mainWindow?.ShowAssistant("Cursor");
                        break;
                    case CHATGPT_HOTKEY_ID:
                        _logger.LogInformation("🎯 Opening ChatGPT");
                        _mainWindow?.ShowAssistant("ChatGPT");
                        break;
                    case CHAT_HOTKEY_ID:
                        _logger.LogInformation("🎯 Opening Chat Window");
                        ShowChatWindow();
                        break;
                    default:
                        _logger.LogWarning($"⚠️ Unknown hotkey ID: {hotkeyId}");
                        break;
                }
                
                handled = true;
            }
        }

        private void ShowChatWindow()
        {
            if (_chatWindow?.Visibility == Visibility.Visible)
            {
                _chatWindow.Hide();
            }
            else
            {
                _chatWindow?.Show();
                _chatWindow?.Activate();
                _chatWindow.Topmost = true;
                _chatWindow.Topmost = false;
            }
        }

        public override void Dispose()
        {
            // Unregister hotkeys
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, KIMI_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, CURSOR_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, CHATGPT_HOTKEY_ID);
                UnregisterHotKey(_windowHandle, CHAT_HOTKEY_ID);
            }
            
            base.Dispose();
        }
    }
}
