using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KimiAppNative.IDE
{
    public partial class IDEMainWindow : Window
    {
        private AIAssistantManager _aiAssistant;
        private CodeEditorManager _codeEditor;
        private ProjectManager _projectManager;
        private BuildManager _buildManager;
        private TerminalManager _terminalManager;
        private FileExplorerManager _fileExplorer;
        private DebugManager _debugManager;
        
        private List<string> _recentProjects = new List<string>();
        private string _currentProjectPath = "";
        private bool _isProjectLoaded = false;

        public IDEMainWindow()
        {
            InitializeComponent();
            InitializeIDE();
            SetupEventHandlers();
        }

        private void InitializeIDE()
        {
            // Initialize all IDE managers
            _aiAssistant = new AIAssistantManager(this);
            _codeEditor = new CodeEditorManager(CodeEditor, EditorTabs, this);
            _projectManager = new ProjectManager(SolutionExplorer, this);
            _buildManager = new BuildManager(OutputTextBox, ErrorList, this);
            _terminalManager = new TerminalManager(TerminalOutput, TerminalInput, this);
            _fileExplorer = new FileExplorerManager(SolutionExplorer, this);
            _debugManager = new DebugManager(this);

            // Setup AI Assistant integration
            _aiAssistant.OnCodeSuggestion += HandleAICodeSuggestion;
            _aiAssistant.OnCodeGeneration += HandleAICodeGeneration;
            _aiAssistant.OnCodeExplanation += HandleAICodeExplanation;
            _aiAssistant.OnBugDetection += HandleAIBugDetection;
            _aiAssistant.OnRefactoringSuggestion += HandleAIRefactoringSuggestion;

            // Initialize UI
            UpdateStatus("IDE Initialized - Ready for AI-powered development");
            LoadRecentProjects();
        }

        private void SetupEventHandlers()
        {
            // Enable window dragging
            this.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };

            // Code editor events
            CodeEditor.TextChanged += CodeEditor_TextChanged;
            CodeEditor.SelectionChanged += CodeEditor_SelectionChanged;
            CodeEditor.KeyDown += CodeEditor_KeyDown;

            // Solution explorer events
            SolutionExplorer.SelectedItemChanged += SolutionExplorer_SelectedItemChanged;

            // Tab events
            EditorTabs.SelectionChanged += EditorTabs_SelectionChanged;
        }

        #region AI Assistant Integration

        private void HandleAICodeSuggestion(string suggestion, int position)
        {
            Dispatcher.Invoke(() =>
            {
                // Show AI suggestion in a popup or inline
                ShowAISuggestionPopup(suggestion, position);
            });
        }

        private void HandleAICodeGeneration(string code, string description)
        {
            Dispatcher.Invoke(() =>
            {
                // Insert generated code at cursor position
                var currentTab = EditorTabs.SelectedItem as TabItem;
                if (currentTab?.Content is Grid grid)
                {
                    var editor = grid.Children.OfType<TextBox>().FirstOrDefault();
                    if (editor != null)
                    {
                        int cursorPos = editor.SelectionStart;
                        editor.Text = editor.Text.Insert(cursorPos, code);
                        editor.SelectionStart = cursorPos + code.Length;
                        editor.Focus();
                    }
                }
                UpdateStatus($"AI Generated: {description}");
            });
        }

        private void HandleAICodeExplanation(string explanation)
        {
            Dispatcher.Invoke(() =>
            {
                // Show code explanation in a popup or side panel
                ShowCodeExplanationPopup(explanation);
            });
        }

        private void HandleAIBugDetection(List<BugReport> bugs)
        {
            Dispatcher.Invoke(() =>
            {
                // Add bugs to error list
                ErrorList.Items.Clear();
                foreach (var bug in bugs)
                {
                    var item = new ListBoxItem
                    {
                        Content = $"🐛 {bug.Severity}: {bug.Description} (Line {bug.Line})",
                        Foreground = bug.Severity == "Error" ? Brushes.Red : 
                                   bug.Severity == "Warning" ? Brushes.Orange : Brushes.Yellow
                    };
                    ErrorList.Items.Add(item);
                }
                UpdateStatus($"AI detected {bugs.Count} potential issues");
            });
        }

        private void HandleAIRefactoringSuggestion(string suggestion, string code)
        {
            Dispatcher.Invoke(() =>
            {
                // Show refactoring suggestion
                ShowRefactoringSuggestion(suggestion, code);
            });
        }

        private void ShowAISuggestionPopup(string suggestion, int position)
        {
            // Create AI suggestion popup
            var popup = new Popup
            {
                PlacementTarget = CodeEditor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                IsOpen = true,
                StaysOpen = false
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8)
            };

            var stackPanel = new StackPanel();
            
            var title = new TextBlock
            {
                Text = "🤖 AI Code Suggestion",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var suggestionText = new TextBlock
            {
                Text = suggestion,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var acceptButton = new Button
            {
                Content = "Accept",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand
            };

            var rejectButton = new Button
            {
                Content = "Reject",
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4),
                Cursor = Cursors.Hand
            };

            acceptButton.Click += (s, e) =>
            {
                // Insert suggestion at cursor
                int cursorPos = CodeEditor.SelectionStart;
                CodeEditor.Text = CodeEditor.Text.Insert(cursorPos, suggestion);
                CodeEditor.SelectionStart = cursorPos + suggestion.Length;
                popup.IsOpen = false;
            };

            rejectButton.Click += (s, e) => popup.IsOpen = false;

            buttonPanel.Children.Add(acceptButton);
            buttonPanel.Children.Add(rejectButton);

            stackPanel.Children.Add(title);
            stackPanel.Children.Add(suggestionText);
            stackPanel.Children.Add(buttonPanel);

            border.Child = stackPanel;
            popup.Child = border;

            // Position popup near cursor
            popup.HorizontalOffset = 10;
            popup.VerticalOffset = 20;
        }

        private void ShowCodeExplanationPopup(string explanation)
        {
            var window = new Window
            {
                Title = "🤖 AI Code Explanation",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10)
            };

            var textBlock = new TextBlock
            {
                Text = explanation,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            scrollViewer.Content = textBlock;
            window.Content = scrollViewer;
            window.Show();
        }

        private void ShowRefactoringSuggestion(string suggestion, string code)
        {
            var window = new Window
            {
                Title = "🔧 AI Refactoring Suggestion",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Suggestion panel
            var suggestionPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5)
            };

            var suggestionText = new TextBlock
            {
                Text = suggestion,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            suggestionPanel.Child = suggestionText;
            Grid.SetRow(suggestionPanel, 0);

            // Code panel
            var codePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5)
            };

            var codeText = new TextBox
            {
                Text = code,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            codePanel.Child = codeText;
            Grid.SetRow(codePanel, 1);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var applyButton = new Button
            {
                Content = "Apply Refactoring",
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

            applyButton.Click += (s, e) =>
            {
                // Apply refactoring to current editor
                CodeEditor.Text = code;
                window.Close();
            };

            cancelButton.Click += (s, e) => window.Close();

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(suggestionPanel);
            grid.Children.Add(codePanel);
            grid.Children.Add(buttonPanel);

            window.Content = grid;
            window.Show();
        }

        #endregion

        #region Event Handlers

        private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Trigger AI analysis when code changes
            _aiAssistant.AnalyzeCode(CodeEditor.Text, CodeEditor.SelectionStart);
            UpdateEditorStatus();
        }

        private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateEditorStatus();
        }

        private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle AI-powered shortcuts
            if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Request AI code completion
                _aiAssistant.RequestCodeCompletion(CodeEditor.Text, CodeEditor.SelectionStart);
                e.Handled = true;
            }
            else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Request AI code explanation
                _aiAssistant.ExplainCode(CodeEditor.Text, CodeEditor.SelectionStart);
                e.Handled = true;
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Request AI refactoring
                _aiAssistant.SuggestRefactoring(CodeEditor.Text, CodeEditor.SelectionStart);
                e.Handled = true;
            }
        }

        private void SolutionExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as TreeViewItem;
            if (selectedItem?.Header?.ToString()?.EndsWith(".cs") == true ||
                selectedItem?.Header?.ToString()?.EndsWith(".xaml") == true)
            {
                // Open file in editor
                string fileName = selectedItem.Header.ToString();
                _fileExplorer.OpenFile(fileName);
            }
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEditorStatus();
        }

        private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string command = TerminalInput.Text.Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    _terminalManager.ExecuteCommand(command);
                    TerminalInput.Clear();
                }
            }
        }

        #endregion

        #region Menu Event Handlers

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            _projectManager.CreateNewProject();
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            _projectManager.OpenProject();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.SaveCurrentFile();
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.SaveAllFiles();
        }

        private void OpenRecentProject_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem?.Header != null)
            {
                _projectManager.OpenProject(menuItem.Header.ToString());
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.Redo();
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.Cut();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.Copy();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.Paste();
        }

        private void FindReplace_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.ShowFindReplace();
        }

        private void ToggleSolutionExplorer_Click(object sender, RoutedEventArgs e)
        {
            // Toggle solution explorer visibility
        }

        private void ToggleProperties_Click(object sender, RoutedEventArgs e)
        {
            // Toggle properties panel visibility
        }

        private void ToggleOutput_Click(object sender, RoutedEventArgs e)
        {
            // Toggle output panel visibility
        }

        private void ToggleErrorList_Click(object sender, RoutedEventArgs e)
        {
            // Toggle error list visibility
        }

        private void ToggleTerminal_Click(object sender, RoutedEventArgs e)
        {
            // Toggle terminal visibility
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            _buildManager.BuildProject();
        }

        private void Rebuild_Click(object sender, RoutedEventArgs e)
        {
            _buildManager.RebuildProject();
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            _buildManager.CleanProject();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            _buildManager.RunProject();
        }

        private void Debug_Click(object sender, RoutedEventArgs e)
        {
            _debugManager.StartDebugging();
        }

        private void StartDebugging_Click(object sender, RoutedEventArgs e)
        {
            _debugManager.StartDebugging();
        }

        private void StartWithoutDebugging_Click(object sender, RoutedEventArgs e)
        {
            _buildManager.RunProject();
        }

        private void StopDebugging_Click(object sender, RoutedEventArgs e)
        {
            _debugManager.StopDebugging();
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            _debugManager.RestartDebugging();
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            // Show options dialog
        }

        private void Extensions_Click(object sender, RoutedEventArgs e)
        {
            // Show extensions manager
        }

        private void AIAssistant_Click(object sender, RoutedEventArgs e)
        {
            _aiAssistant.ShowAIAssistantPanel();
        }

        private void Documentation_Click(object sender, RoutedEventArgs e)
        {
            // Open documentation
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("OhGees IDE v1.0\nAI-Powered Development Environment\nBuilt with C# and WPF", 
                          "About OhGees IDE", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Toolbar Event Handlers

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.CreateNewFile();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.OpenFile();
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            _codeEditor.SaveCurrentFile();
        }

        #endregion

        #region Window Event Handlers

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region Helper Methods

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        private void UpdateEditorStatus()
        {
            var lines = CodeEditor.Text.Split('\n');
            var currentLine = CodeEditor.Text.Substring(0, CodeEditor.SelectionStart).Split('\n').Length;
            var currentColumn = CodeEditor.SelectionStart - CodeEditor.Text.LastIndexOf('\n', CodeEditor.SelectionStart - 1);
            
            EditorStatus.Text = $"Line {currentLine}, Column {currentColumn}";
        }

        private void LoadRecentProjects()
        {
            // Load recent projects from settings
            // Implementation would load from user settings
        }

        #endregion
    }

    #region Supporting Classes

    public class BugReport
    {
        public string Severity { get; set; }
        public string Description { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    #endregion
}
