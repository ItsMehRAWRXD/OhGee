using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Windows.Markup;
using System.Xml;

namespace KimiAppNative
{
    public partial class GuiTemplateCreator : Window
    {
        private FrameworkElement _selectedElement;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private List<FrameworkElement> _designElements = new List<FrameworkElement>();
        private Stack<DesignAction> _undoStack = new Stack<DesignAction>();
        private Stack<DesignAction> _redoStack = new Stack<DesignAction>();
        private int _componentCounter = 0;

        public GuiTemplateCreator()
        {
            InitializeComponent();
            InitializeDesigner();
            SetupEventHandlers();
        }

        private void InitializeDesigner()
        {
            // Enable window dragging
            this.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };

            // Setup canvas events
            DesignCanvas.MouseMove += DesignCanvas_MouseMove;
            DesignCanvas.MouseLeftButtonDown += DesignCanvas_MouseLeftButtonDown;
            DesignCanvas.MouseLeftButtonUp += DesignCanvas_MouseLeftButtonUp;
            DesignCanvas.Drop += DesignCanvas_Drop;

            // Setup drag and drop for components
            SetupDragAndDrop();
        }

        private void SetupEventHandlers()
        {
            // Handle window state changes
            this.StateChanged += (sender, e) =>
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.Hide();
                }
            };
        }

        private void SetupDragAndDrop()
        {
            // Make all component buttons draggable
            var componentButtons = FindVisualChildren<Button>(this)
                .Where(b => b.Tag != null && b.Tag.ToString().Contains("Component"))
                .ToList();

            foreach (var button in componentButtons)
            {
                button.PreviewMouseLeftButtonDown += ComponentButton_PreviewMouseLeftButtonDown;
                button.PreviewMouseMove += ComponentButton_PreviewMouseMove;
            }
        }

        private void ComponentButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void ComponentButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    var button = sender as Button;
                    if (button != null)
                    {
                        DragDrop.DoDragDrop(button, button.Tag.ToString(), DragDropEffects.Copy);
                    }
                }
            }
        }

        private void DesignCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)))
            {
                string componentType = e.Data.GetData(typeof(string)) as string;
                Point dropPosition = e.GetPosition(DesignCanvas);
                CreateComponent(componentType, dropPosition);
            }
        }

        private void CreateComponent(string componentType, Point position)
        {
            FrameworkElement element = null;

            switch (componentType)
            {
                case "Button":
                    element = CreateButton();
                    break;
                case "TextBox":
                    element = CreateTextBox();
                    break;
                case "Label":
                    element = CreateLabel();
                    break;
                case "CheckBox":
                    element = CreateCheckBox();
                    break;
                case "RadioButton":
                    element = CreateRadioButton();
                    break;
                case "ComboBox":
                    element = CreateComboBox();
                    break;
                case "ListBox":
                    element = CreateListBox();
                    break;
                case "Grid":
                    element = CreateGrid();
                    break;
                case "StackPanel":
                    element = CreateStackPanel();
                    break;
                case "DataGrid":
                    element = CreateDataGrid();
                    break;
                case "TreeView":
                    element = CreateTreeView();
                    break;
                case "TabControl":
                    element = CreateTabControl();
                    break;
                case "Image":
                    element = CreateImage();
                    break;
                case "WebView2":
                    element = CreateWebView2();
                    break;
                default:
                    MessageBox.Show($"Component type '{componentType}' not implemented yet.", "Not Implemented", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
            }

            if (element != null)
            {
                // Set position
                Canvas.SetLeft(element, position.X);
                Canvas.SetTop(element, position.Y);

                // Add to canvas
                DesignCanvas.Children.Add(element);
                _designElements.Add(element);

                // Setup selection
                element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                element.Tag = $"Component_{++_componentCounter}";

                // Hide welcome message
                WelcomeMessage.Visibility = Visibility.Collapsed;

                // Update status
                UpdateStatus();
                UpdateComponentCount();

                // Add to undo stack
                _undoStack.Push(new DesignAction { Type = "Add", Element = element, Position = position });
                _redoStack.Clear();
            }
        }

        private FrameworkElement CreateButton()
        {
            var button = new Button
            {
                Content = "Button",
                Width = 100,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            return button;
        }

        private FrameworkElement CreateTextBox()
        {
            var textBox = new TextBox
            {
                Text = "TextBox",
                Width = 150,
                Height = 25,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            return textBox;
        }

        private FrameworkElement CreateLabel()
        {
            var label = new Label
            {
                Content = "Label",
                Width = 100,
                Height = 25,
                Foreground = Brushes.Black,
                Background = Brushes.Transparent
            };
            return label;
        }

        private FrameworkElement CreateCheckBox()
        {
            var checkBox = new CheckBox
            {
                Content = "CheckBox",
                Width = 100,
                Height = 25,
                Foreground = Brushes.Black
            };
            return checkBox;
        }

        private FrameworkElement CreateRadioButton()
        {
            var radioButton = new RadioButton
            {
                Content = "RadioButton",
                Width = 100,
                Height = 25,
                Foreground = Brushes.Black
            };
            return radioButton;
        }

        private FrameworkElement CreateComboBox()
        {
            var comboBox = new ComboBox
            {
                Width = 150,
                Height = 25,
                Background = Brushes.White,
                Foreground = Brushes.Black
            };
            comboBox.Items.Add("Item 1");
            comboBox.Items.Add("Item 2");
            comboBox.Items.Add("Item 3");
            comboBox.SelectedIndex = 0;
            return comboBox;
        }

        private FrameworkElement CreateListBox()
        {
            var listBox = new ListBox
            {
                Width = 150,
                Height = 100,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            listBox.Items.Add("Item 1");
            listBox.Items.Add("Item 2");
            listBox.Items.Add("Item 3");
            return listBox;
        }

        private FrameworkElement CreateGrid()
        {
            var border = new Border
            {
                Width = 200,
                Height = 150,
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 212)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                BorderThickness = new Thickness(2)
            };
            
            var grid = new Grid();
            
            // Add some default rows and columns
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            
            border.Child = grid;
            return border;
        }

        private FrameworkElement CreateStackPanel()
        {
            var border = new Border
            {
                Width = 150,
                Height = 100,
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 212)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                BorderThickness = new Thickness(2)
            };
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Add some default children
            stackPanel.Children.Add(new Label { Content = "StackPanel", HorizontalAlignment = HorizontalAlignment.Center });
            border.Child = stackPanel;
            return border;
        }

        private FrameworkElement CreateDataGrid()
        {
            var dataGrid = new DataGrid
            {
                Width = 200,
                Height = 150,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                AutoGenerateColumns = true
            };
            return dataGrid;
        }

        private FrameworkElement CreateTreeView()
        {
            var treeView = new TreeView
            {
                Width = 150,
                Height = 150,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            var rootItem = new TreeViewItem { Header = "Root" };
            rootItem.Items.Add(new TreeViewItem { Header = "Child 1" });
            rootItem.Items.Add(new TreeViewItem { Header = "Child 2" });
            treeView.Items.Add(rootItem);

            return treeView;
        }

        private FrameworkElement CreateTabControl()
        {
            var tabControl = new TabControl
            {
                Width = 200,
                Height = 150,
                Background = Brushes.White,
                Foreground = Brushes.Black
            };

            var tab1 = new TabItem { Header = "Tab 1" };
            tab1.Content = new Label { Content = "Tab 1 Content" };
            var tab2 = new TabItem { Header = "Tab 2" };
            tab2.Content = new Label { Content = "Tab 2 Content" };

            tabControl.Items.Add(tab1);
            tabControl.Items.Add(tab2);

            return tabControl;
        }

        private FrameworkElement CreateImage()
        {
            var border = new Border
            {
                Width = 100,
                Height = 100,
                Background = Brushes.LightGray,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2)
            };
            
            var image = new Image
            {
                Stretch = Stretch.Uniform
            };

            // Add a placeholder rectangle
            image.Source = new DrawingImage(new GeometryDrawing(Brushes.LightGray, new Pen(Brushes.Gray, 2), 
                new RectangleGeometry(new Rect(0, 0, 100, 100))));

            border.Child = image;
            return border;
        }

        private FrameworkElement CreateWebView2()
        {
            var border = new Border
            {
                Width = 200,
                Height = 150,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            
            var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            border.Child = webView;
            return border;
        }

        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectElement(sender as FrameworkElement);
            e.Handled = true;
        }

        private void SelectElement(FrameworkElement element)
        {
            // Remove previous selection
            if (_selectedElement != null)
            {
                if (_selectedElement is Border border)
                {
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                }
            }

            // Select new element
            _selectedElement = element;
            if (_selectedElement != null)
            {
                if (_selectedElement is Border border)
                {
                    border.BorderBrush = new SolidColorBrush(Colors.Red);
                    border.BorderThickness = new Thickness(2);
                }
                ShowProperties(_selectedElement);
            }
        }

        private void ShowProperties(FrameworkElement element)
        {
            PropertiesPanel.Children.Clear();

            if (element == null) return;

            // Add common properties
            AddPropertyField("Name", element.Name, (value) => element.Name = value);
            AddPropertyField("Width", element.Width.ToString(), (value) => 
            {
                if (double.TryParse(value, out double width))
                    element.Width = width;
            });
            AddPropertyField("Height", element.Height.ToString(), (value) => 
            {
                if (double.TryParse(value, out double height))
                    element.Height = height;
            });

            // Add specific properties based on element type
            if (element is Button button)
            {
                AddPropertyField("Content", button.Content?.ToString() ?? "", (value) => button.Content = value);
            }
            else if (element is TextBox textBox)
            {
                AddPropertyField("Text", textBox.Text, (value) => textBox.Text = value);
            }
            else if (element is Label label)
            {
                AddPropertyField("Content", label.Content?.ToString() ?? "", (value) => label.Content = value);
            }
        }

        private void AddPropertyField(string propertyName, string currentValue, Action<string> setter)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            
            var label = new Label
            {
                Content = propertyName + ":",
                Width = 80,
                Foreground = Brushes.White
            };

            var textBox = new TextBox
            {
                Text = currentValue,
                Width = 120,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray
            };

            textBox.TextChanged += (s, e) => setter(textBox.Text);

            panel.Children.Add(label);
            panel.Children.Add(textBox);
            PropertiesPanel.Children.Add(panel);
        }

        private void DesignCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(DesignCanvas);
            PositionText.Text = $"Position: {(int)position.X}, {(int)position.Y}";
        }

        private void DesignCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == DesignCanvas)
            {
                SelectElement(null);
            }
        }

        private void DesignCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Handle drag end
        }

        private void UpdateStatus()
        {
            StatusText.Text = $"Components: {_designElements.Count}";
        }

        private void UpdateComponentCount()
        {
            ComponentCountText.Text = $"Components: {_designElements.Count}";
        }

        // Event Handlers
        private void ComponentButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null)
            {
                CreateComponent(button.Tag.ToString(), new Point(50, 50));
            }
        }

        private void TemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null)
            {
                LoadTemplate(button.Tag.ToString());
            }
        }

        private void LoadTemplate(string templateName)
        {
            // Clear current design
            DesignCanvas.Children.Clear();
            _designElements.Clear();
            _selectedElement = null;

            switch (templateName)
            {
                case "LoginForm":
                    CreateLoginFormTemplate();
                    break;
                case "DataEntry":
                    CreateDataEntryTemplate();
                    break;
                case "Dashboard":
                    CreateDashboardTemplate();
                    break;
                case "Settings":
                    CreateSettingsTemplate();
                    break;
                case "ChatInterface":
                    CreateChatInterfaceTemplate();
                    break;
            }

            WelcomeMessage.Visibility = Visibility.Collapsed;
            UpdateStatus();
        }

        private void CreateLoginFormTemplate()
        {
            var loginBorder = new Border
            {
                Width = 300,
                Height = 200,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            
            var loginPanel = new StackPanel();

            var titleLabel = new Label
            {
                Content = "Login Form",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var usernameLabel = new Label { Content = "Username:", Margin = new Thickness(10, 5) };
            var usernameBox = new TextBox { Margin = new Thickness(10, 0, 10, 5) };

            var passwordLabel = new Label { Content = "Password:", Margin = new Thickness(10, 5) };
            var passwordBox = new PasswordBox { Margin = new Thickness(10, 0, 10, 5) };

            var loginButton = new Button
            {
                Content = "Login",
                Width = 100,
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = Brushes.White
            };

            loginPanel.Children.Add(titleLabel);
            loginPanel.Children.Add(usernameLabel);
            loginPanel.Children.Add(usernameBox);
            loginPanel.Children.Add(passwordLabel);
            loginPanel.Children.Add(passwordBox);
            loginPanel.Children.Add(loginButton);

            loginBorder.Child = loginPanel;
            Canvas.SetLeft(loginBorder, 50);
            Canvas.SetTop(loginBorder, 50);
            DesignCanvas.Children.Add(loginBorder);
            _designElements.Add(loginBorder);
        }

        private void CreateDataEntryTemplate()
        {
            var dataBorder = new Border
            {
                Width = 400,
                Height = 300,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            
            var dataPanel = new Grid();

            dataPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            dataPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            dataPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            var titleLabel = new Label
            {
                Content = "Data Entry Form",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(titleLabel, 0);

            var contentPanel = new StackPanel { Margin = new Thickness(10) };
            Grid.SetRow(contentPanel, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };
            Grid.SetRow(buttonPanel, 2);

            var saveButton = new Button { Content = "Save", Width = 80, Height = 30, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(5) };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);

            dataPanel.Children.Add(titleLabel);
            dataPanel.Children.Add(contentPanel);
            dataPanel.Children.Add(buttonPanel);

            dataBorder.Child = dataPanel;
            Canvas.SetLeft(dataBorder, 50);
            Canvas.SetTop(dataBorder, 50);
            DesignCanvas.Children.Add(dataBorder);
            _designElements.Add(dataBorder);
        }

        private void CreateDashboardTemplate()
        {
            var dashboardBorder = new Border
            {
                Width = 500,
                Height = 400,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            
            var dashboard = new Grid();

            dashboard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            dashboard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var headerLabel = new Label
            {
                Content = "Dashboard",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(headerLabel, 0);
            Grid.SetColumnSpan(headerLabel, 2);

            var sidebar = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                Margin = new Thickness(1, 0, 0, 0)
            };
            Grid.SetRow(sidebar, 1);
            Grid.SetColumn(sidebar, 0);

            var mainContent = new Label
            {
                Content = "Main Content Area",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            Grid.SetRow(mainContent, 1);
            Grid.SetColumn(mainContent, 1);

            dashboard.Children.Add(headerLabel);
            dashboard.Children.Add(sidebar);
            dashboard.Children.Add(mainContent);

            dashboardBorder.Child = dashboard;
            Canvas.SetLeft(dashboardBorder, 50);
            Canvas.SetTop(dashboardBorder, 50);
            DesignCanvas.Children.Add(dashboardBorder);
            _designElements.Add(dashboardBorder);
        }

        private void CreateSettingsTemplate()
        {
            var settingsPanel = new TabControl
            {
                Width = 400,
                Height = 300,
                Background = Brushes.White
            };

            var generalTab = new TabItem { Header = "General" };
            var advancedTab = new TabItem { Header = "Advanced" };
            var aboutTab = new TabItem { Header = "About" };

            settingsPanel.Items.Add(generalTab);
            settingsPanel.Items.Add(advancedTab);
            settingsPanel.Items.Add(aboutTab);

            Canvas.SetLeft(settingsPanel, 50);
            Canvas.SetTop(settingsPanel, 50);
            DesignCanvas.Children.Add(settingsPanel);
            _designElements.Add(settingsPanel);
        }

        private void CreateChatInterfaceTemplate()
        {
            var chatBorder = new Border
            {
                Width = 400,
                Height = 500,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            
            var chatPanel = new Grid();

            chatPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            chatPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            chatPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });

            var headerLabel = new Label
            {
                Content = "Chat Interface",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(headerLabel, 0);

            var messageList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                BorderThickness = new Thickness(0)
            };
            Grid.SetRow(messageList, 1);

            var inputPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };
            Grid.SetRow(inputPanel, 2);

            var messageBox = new TextBox
            {
                Width = 300,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };

            var sendButton = new Button
            {
                Content = "Send",
                Width = 80,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = Brushes.White
            };

            inputPanel.Children.Add(messageBox);
            inputPanel.Children.Add(sendButton);

            chatPanel.Children.Add(headerLabel);
            chatPanel.Children.Add(messageList);
            chatPanel.Children.Add(inputPanel);

            chatBorder.Child = chatPanel;
            Canvas.SetLeft(chatBorder, 50);
            Canvas.SetTop(chatBorder, 50);
            DesignCanvas.Children.Add(chatBorder);
            _designElements.Add(chatBorder);
        }

        // Toolbar Event Handlers
        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Create a new project? All unsaved changes will be lost.", "New Project", 
                              MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DesignCanvas.Children.Clear();
                _designElements.Clear();
                _selectedElement = null;
                WelcomeMessage.Visibility = Visibility.Visible;
                UpdateStatus();
            }
        }

        private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "XAML Files (*.xaml)|*.xaml|All Files (*.*)|*.*",
                Title = "Open GUI Project"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    LoadProject(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading project: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "XAML Files (*.xaml)|*.xaml|All Files (*.*)|*.*",
                Title = "Save GUI Project"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    SaveProject(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving project: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var action = _undoStack.Pop();
                _redoStack.Push(action);

                if (action.Type == "Add")
                {
                    DesignCanvas.Children.Remove(action.Element);
                    _designElements.Remove(action.Element);
                }

                UpdateStatus();
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count > 0)
            {
                var action = _redoStack.Pop();
                _undoStack.Push(action);

                if (action.Type == "Add")
                {
                    Canvas.SetLeft(action.Element, action.Position.X);
                    Canvas.SetTop(action.Element, action.Position.Y);
                    DesignCanvas.Children.Add(action.Element);
                    _designElements.Add(action.Element);
                }

                UpdateStatus();
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateXamlCode();
            MessageBox.Show("Preview functionality would show the generated XAML in a preview window.", 
                          "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GenerateCodeButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateXamlCode();
            GenerateCSharpCode();
            MessageBox.Show("Code generated successfully! Check the XAML and C# tabs.", 
                          "Code Generated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GenerateXamlCode()
        {
            var xaml = new StringBuilder();
            xaml.AppendLine("<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
            xaml.AppendLine("        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
            xaml.AppendLine("        Title=\"Generated GUI\" Height=\"600\" Width=\"800\">");
            xaml.AppendLine("    <Grid>");

            foreach (var element in _designElements)
            {
                var left = Canvas.GetLeft(element);
                var top = Canvas.GetTop(element);
                
                xaml.AppendLine($"        <{element.GetType().Name} Canvas.Left=\"{left}\" Canvas.Top=\"{top}\"");
                xaml.AppendLine($"                  Width=\"{element.Width}\" Height=\"{element.Height}\"");
                
                if (element is Button button)
                {
                    xaml.AppendLine($"                  Content=\"{button.Content}\"");
                }
                else if (element is TextBox textBox)
                {
                    xaml.AppendLine($"                  Text=\"{textBox.Text}\"");
                }
                else if (element is Label label)
                {
                    xaml.AppendLine($"                  Content=\"{label.Content}\"");
                }
                
                xaml.AppendLine("                  />");
            }

            xaml.AppendLine("    </Grid>");
            xaml.AppendLine("</Window>");

            XamlCodeTextBox.Text = xaml.ToString();
        }

        private void GenerateCSharpCode()
        {
            var csharp = new StringBuilder();
            csharp.AppendLine("using System.Windows;");
            csharp.AppendLine("using System.Windows.Controls;");
            csharp.AppendLine("using System.Windows.Media;");
            csharp.AppendLine();
            csharp.AppendLine("namespace GeneratedGUI");
            csharp.AppendLine("{");
            csharp.AppendLine("    public partial class MainWindow : Window");
            csharp.AppendLine("    {");
            csharp.AppendLine("        public MainWindow()");
            csharp.AppendLine("        {");
            csharp.AppendLine("            InitializeComponent();");
            csharp.AppendLine("        }");
            csharp.AppendLine("    }");
            csharp.AppendLine("}");

            CSharpCodeTextBox.Text = csharp.ToString();
        }

        private void ExportXamlButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "XAML Files (*.xaml)|*.xaml",
                Title = "Export XAML"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, XamlCodeTextBox.Text);
                MessageBox.Show("XAML exported successfully!", "Export Complete", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportCSharpButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "C# Files (*.cs)|*.cs",
                Title = "Export C# Code"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, CSharpCodeTextBox.Text);
                MessageBox.Show("C# code exported successfully!", "Export Complete", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CreateProjectButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This would create a complete Visual Studio project with the generated GUI.", 
                          "Create Project", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadProject(string fileName)
        {
            // Implementation for loading XAML projects
            MessageBox.Show($"Loading project from: {fileName}", "Load Project", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveProject(string fileName)
        {
            GenerateXamlCode();
            File.WriteAllText(fileName, XamlCodeTextBox.Text);
            MessageBox.Show("Project saved successfully!", "Save Complete", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Window Event Handlers
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Helper Methods
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }

    public class DesignAction
    {
        public string Type { get; set; }
        public FrameworkElement Element { get; set; }
        public Point Position { get; set; }
    }
}
