using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;

namespace KimiAppNative
{
    public class SystemTrayManager
    {
        private TaskbarIcon _taskbarIcon;
        private MainWindow _mainWindow;

        public SystemTrayManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _taskbarIcon = new TaskbarIcon();
            
            // Try to load icon, fallback to default if not found
            try
            {
                if (System.IO.File.Exists("kimi.ico"))
                {
                    _taskbarIcon.Icon = new System.Drawing.Icon("kimi.ico");
                }
                else
                {
                    // Use default system icon as fallback
                    _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            
            _taskbarIcon.ToolTipText = "OhGee - AI Assistant Hub - Ctrl+Shift+G (Kimi), Ctrl+Shift+C (Cursor), Ctrl+Shift+H (ChatGPT), Ctrl+Shift+A (Chat)";
            
            // Create context menu
            var contextMenu = new ContextMenu();
            
            // Kimi AI menu item
            var kimiMenuItem = new MenuItem
            {
                Header = "🧠 Open Kimi AI",
                Icon = new TextBlock { Text = "🧠", FontSize = 16 }
            };
            kimiMenuItem.Click += (s, e) => _mainWindow.ShowAssistant("Kimi");
            
            // Cursor menu item
            var cursorMenuItem = new MenuItem
            {
                Header = "🎯 Open Cursor",
                Icon = new TextBlock { Text = "🎯", FontSize = 16 }
            };
            cursorMenuItem.Click += (s, e) => _mainWindow.ShowAssistant("Cursor");
            
            // ChatGPT menu item
            var chatgptMenuItem = new MenuItem
            {
                Header = "💬 Open ChatGPT",
                Icon = new TextBlock { Text = "💬", FontSize = 16 }
            };
            chatgptMenuItem.Click += (s, e) => _mainWindow.ShowAssistant("ChatGPT");
            
            // Chat Assistant menu item
            var chatMenuItem = new MenuItem
            {
                Header = "🤖 Open Chat Assistant",
                Icon = new TextBlock { Text = "🤖", FontSize = 16 }
            };
            chatMenuItem.Click += (s, e) => ShowChatWindow();
            
            // Separator
            var separator = new Separator();
            
            // Exit menu item
            var exitMenuItem = new MenuItem
            {
                Header = "Exit",
                Icon = new TextBlock { Text = "❌", FontSize = 16 }
            };
            exitMenuItem.Click += (s, e) => Application.Current.Shutdown();
            
            // Add items to context menu
            contextMenu.Items.Add(kimiMenuItem);
            contextMenu.Items.Add(cursorMenuItem);
            contextMenu.Items.Add(chatgptMenuItem);
            contextMenu.Items.Add(chatMenuItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(exitMenuItem);
            
            _taskbarIcon.ContextMenu = contextMenu;
            
            // Handle double-click to show Kimi
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => _mainWindow.ShowAssistant("Kimi");
            
            // Handle left-click to show/hide window
            _taskbarIcon.TrayLeftMouseUp += (s, e) =>
            {
                if (_mainWindow.Visibility == Visibility.Visible)
                {
                    _mainWindow.Hide();
                }
                else
                {
                    _mainWindow.ShowAssistant("Kimi");
                }
            };
        }

        private void ShowChatWindow()
        {
            // Find the chat window from the application
            var chatWindow = Application.Current.Windows.OfType<ChatWindow>().FirstOrDefault();
            if (chatWindow != null)
            {
                if (chatWindow.Visibility == Visibility.Visible)
                {
                    chatWindow.Hide();
                }
                else
                {
                    chatWindow.Show();
                    chatWindow.Activate();
                    chatWindow.Topmost = true;
                    chatWindow.Topmost = false;
                }
            }
        }

        public void Dispose()
        {
            _taskbarIcon?.Dispose();
        }
    }
}
