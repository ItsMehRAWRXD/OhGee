using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace KimiAppNative.IDE
{
    public class CodeEditorManager
    {
        private readonly TextBox _mainEditor;
        private readonly TabControl _tabControl;
        private readonly IDEMainWindow _ideWindow;
        private readonly Dictionary<string, EditorTab> _openFiles;
        private EditorTab _currentTab;

        public CodeEditorManager(TextBox mainEditor, TabControl tabControl, IDEMainWindow ideWindow)
        {
            _mainEditor = mainEditor;
            _tabControl = tabControl;
            _ideWindow = ideWindow;
            _openFiles = new Dictionary<string, EditorTab>();
            
            SetupEditor();
        }

        private void SetupEditor()
        {
            // Configure main editor
            _mainEditor.FontFamily = new FontFamily("Consolas");
            _mainEditor.FontSize = 14;
            _mainEditor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            _mainEditor.Foreground = Brushes.White;
            _mainEditor.BorderThickness = new Thickness(0);
            _mainEditor.AcceptsReturn = true;
            _mainEditor.AcceptsTab = true;
            _mainEditor.TextWrapping = TextWrapping.NoWrap;
            _mainEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _mainEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

            // Setup syntax highlighting
            SetupSyntaxHighlighting();
        }

        private void SetupSyntaxHighlighting()
        {
            // Basic syntax highlighting for C#
            _mainEditor.TextChanged += (sender, e) =>
            {
                ApplySyntaxHighlighting();
            };
        }

        private void ApplySyntaxHighlighting()
        {
            // This is a simplified syntax highlighting
            // In a full implementation, you'd use a proper syntax highlighting library
            var text = _mainEditor.Text;
            
            // Store cursor position
            int cursorPosition = _mainEditor.SelectionStart;
            
            // Apply basic highlighting (this is a simplified version)
            // In reality, you'd want to use a proper syntax highlighting control
            
            // Restore cursor position
            _mainEditor.SelectionStart = cursorPosition;
        }

        #region File Operations

        public void CreateNewFile()
        {
            var fileName = $"NewFile{_openFiles.Count + 1}.cs";
            var tab = new EditorTab
            {
                FileName = fileName,
                FilePath = "",
                Content = GetDefaultCodeTemplate("C#"),
                IsModified = false
            };

            AddTab(tab);
        }

        public void OpenFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "C# Files (*.cs)|*.cs|XAML Files (*.xaml)|*.xaml|All Files (*.*)|*.*",
                Title = "Open File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                OpenFile(openFileDialog.FileName);
            }
        }

        public void OpenFile(string filePath)
        {
            if (_openFiles.ContainsKey(filePath))
            {
                // File already open, switch to it
                SwitchToTab(filePath);
                return;
            }

            try
            {
                var content = File.ReadAllText(filePath);
                var fileName = Path.GetFileName(filePath);
                
                var tab = new EditorTab
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Content = content,
                    IsModified = false
                };

                AddTab(tab);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveCurrentFile()
        {
            if (_currentTab != null)
            {
                SaveFile(_currentTab);
            }
        }

        public void SaveAllFiles()
        {
            foreach (var tab in _openFiles.Values.Where(t => t.IsModified))
            {
                SaveFile(tab);
            }
        }

        private void SaveFile(EditorTab tab)
        {
            try
            {
                if (string.IsNullOrEmpty(tab.FilePath))
                {
                    // New file, show save dialog
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "C# Files (*.cs)|*.cs|XAML Files (*.xaml)|*.xaml|All Files (*.*)|*.*",
                        Title = "Save File"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        tab.FilePath = saveFileDialog.FileName;
                        tab.FileName = Path.GetFileName(tab.FilePath);
                    }
                    else
                    {
                        return; // User cancelled
                    }
                }

                File.WriteAllText(tab.FilePath, tab.Content);
                tab.IsModified = false;
                UpdateTabHeader(tab);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Tab Management

        private void AddTab(EditorTab tab)
        {
            _openFiles[tab.FilePath] = tab;

            var tabItem = new TabItem
            {
                Header = CreateTabHeader(tab),
                Content = CreateTabContent(tab)
            };

            tabItem.Tag = tab;
            _tabControl.Items.Add(tabItem);
            _tabControl.SelectedItem = tabItem;
            _currentTab = tab;

            // Update main editor
            _mainEditor.Text = tab.Content;
        }

        private StackPanel CreateTabHeader(EditorTab tab)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var fileName = new TextBlock
            {
                Text = tab.FileName,
                Foreground = Brushes.White,
                FontSize = 12
            };

            var closeButton = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Width = 20,
                Height = 20,
                FontSize = 10,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            closeButton.Click += (s, e) => CloseTab(tab);

            header.Children.Add(fileName);
            header.Children.Add(closeButton);

            return header;
        }

        private Grid CreateTabContent(EditorTab tab)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var editor = new TextBox
            {
                Text = tab.Content,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                BorderThickness = new Thickness(0),
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            editor.TextChanged += (s, e) =>
            {
                tab.Content = editor.Text;
                tab.IsModified = true;
                UpdateTabHeader(tab);
            };

            editor.SelectionChanged += (s, e) =>
            {
                // Update main editor when selection changes
                _mainEditor.Text = editor.Text;
                _mainEditor.SelectionStart = editor.SelectionStart;
                _mainEditor.SelectionLength = editor.SelectionLength;
            };

            Grid.SetRow(editor, 0);
            grid.Children.Add(editor);

            // Status bar for this tab
            var statusBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                Height = 25
            };

            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };

            var statusText = new TextBlock
            {
                Text = $"Line 1, Column 1 | {GetFileType(tab.FileName)}",
                Foreground = Brushes.White,
                FontSize = 11
            };

            statusPanel.Children.Add(statusText);
            statusBar.Child = statusPanel;
            Grid.SetRow(statusBar, 1);
            grid.Children.Add(statusBar);

            return grid;
        }

        private void UpdateTabHeader(EditorTab tab)
        {
            var tabItem = _tabControl.Items.Cast<TabItem>()
                .FirstOrDefault(t => t.Tag == tab);

            if (tabItem != null)
            {
                tabItem.Header = CreateTabHeader(tab);
            }
        }

        private void SwitchToTab(string filePath)
        {
            var tabItem = _tabControl.Items.Cast<TabItem>()
                .FirstOrDefault(t => ((EditorTab)t.Tag).FilePath == filePath);

            if (tabItem != null)
            {
                _tabControl.SelectedItem = tabItem;
                _currentTab = (EditorTab)tabItem.Tag;
                _mainEditor.Text = _currentTab.Content;
            }
        }

        private void CloseTab(EditorTab tab)
        {
            if (tab.IsModified)
            {
                var result = MessageBox.Show(
                    $"Save changes to {tab.FileName}?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveFile(tab);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return; // Don't close
                }
            }

            _openFiles.Remove(tab.FilePath);

            var tabItem = _tabControl.Items.Cast<TabItem>()
                .FirstOrDefault(t => t.Tag == tab);

            if (tabItem != null)
            {
                _tabControl.Items.Remove(tabItem);
            }

            if (_currentTab == tab)
            {
                _currentTab = null;
                _mainEditor.Text = "";
            }
        }

        #endregion

        #region Editor Operations

        public void Undo()
        {
            _mainEditor.Undo();
        }

        public void Redo()
        {
            _mainEditor.Redo();
        }

        public void Cut()
        {
            _mainEditor.Cut();
        }

        public void Copy()
        {
            _mainEditor.Copy();
        }

        public void Paste()
        {
            _mainEditor.Paste();
        }

        public void ShowFindReplace()
        {
            // Show find and replace dialog
            var window = new Window
            {
                Title = "Find and Replace",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _ideWindow,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var findLabel = new TextBlock
            {
                Text = "Find:",
                Foreground = Brushes.White,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var findTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5)
            };

            var replaceLabel = new TextBlock
            {
                Text = "Replace:",
                Foreground = Brushes.White,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var replaceTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var findButton = new Button
            {
                Content = "Find",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 8),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            var replaceButton = new Button
            {
                Content = "Replace",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 8),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 8),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            findButton.Click += (s, e) =>
            {
                var text = findTextBox.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    var index = _mainEditor.Text.IndexOf(text, _mainEditor.SelectionStart + _mainEditor.SelectionLength);
                    if (index >= 0)
                    {
                        _mainEditor.SelectionStart = index;
                        _mainEditor.SelectionLength = text.Length;
                        _mainEditor.Focus();
                    }
                    else
                    {
                        MessageBox.Show("Text not found", "Find", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            };

            replaceButton.Click += (s, e) =>
            {
                var findText = findTextBox.Text;
                var replaceText = replaceTextBox.Text;
                if (!string.IsNullOrEmpty(findText))
                {
                    _mainEditor.Text = _mainEditor.Text.Replace(findText, replaceText);
                }
            };

            cancelButton.Click += (s, e) => window.Close();

            buttonPanel.Children.Add(findButton);
            buttonPanel.Children.Add(replaceButton);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(findLabel, 0);
            Grid.SetRow(findTextBox, 1);
            Grid.SetRow(replaceLabel, 2);
            Grid.SetRow(replaceTextBox, 3);
            Grid.SetRow(buttonPanel, 4);

            grid.Children.Add(findLabel);
            grid.Children.Add(findTextBox);
            grid.Children.Add(replaceLabel);
            grid.Children.Add(replaceTextBox);
            grid.Children.Add(buttonPanel);

            window.Content = grid;
            window.Show();
        }

        #endregion

        #region Helper Methods

        private string GetDefaultCodeTemplate(string language)
        {
            return language switch
            {
                "C#" => @"using System;

namespace MyProject
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello, OhGees IDE!"");
        }
    }
}",
                "XAML" => @"<Window x:Class=""MyProject.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""MainWindow"" Height=""450"" Width=""800"">
    <Grid>
        
    </Grid>
</Window>",
                _ => "// New file"
            };
        }

        private string GetFileType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".cs" => "C#",
                ".xaml" => "XAML",
                ".xml" => "XML",
                ".json" => "JSON",
                ".js" => "JavaScript",
                ".html" => "HTML",
                ".css" => "CSS",
                _ => "Text"
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class EditorTab
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Content { get; set; }
        public bool IsModified { get; set; }
    }

    #endregion
}
