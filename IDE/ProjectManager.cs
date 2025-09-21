using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.Win32;
using System.Text.Json;

namespace KimiAppNative.IDE
{
    public class ProjectManager
    {
        private readonly TreeView _solutionExplorer;
        private readonly IDEMainWindow _ideWindow;
        private Project _currentProject;
        private string _currentProjectPath;

        public ProjectManager(TreeView solutionExplorer, IDEMainWindow ideWindow)
        {
            _solutionExplorer = solutionExplorer;
            _ideWindow = ideWindow;
            SetupSolutionExplorer();
        }

        private void SetupSolutionExplorer()
        {
            _solutionExplorer.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            _solutionExplorer.Foreground = Brushes.White;
            _solutionExplorer.BorderThickness = new Thickness(0);
        }

        #region Project Operations

        public void CreateNewProject()
        {
            var window = new Window
            {
                Title = "New Project",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _ideWindow,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Project Templates
            var templateLabel = new TextBlock
            {
                Text = "Project Template:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var templateComboBox = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5),
                Height = 30
            };

            templateComboBox.Items.Add("WPF Application");
            templateComboBox.Items.Add("Console Application");
            templateComboBox.Items.Add("Class Library");
            templateComboBox.Items.Add("Empty Project");
            templateComboBox.SelectedIndex = 0;

            // Project Name
            var nameLabel = new TextBlock
            {
                Text = "Project Name:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var nameTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5),
                Height = 30,
                Text = "MyProject"
            };

            // Location
            var locationLabel = new TextBlock
            {
                Text = "Location:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var locationPanel = new Grid();
            locationPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            locationPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var locationTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5, 5, 5),
                Height = 30,
                Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\OhGeesProjects"
            };

            var browseButton = new Button
            {
                Content = "Browse...",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(5, 5, 10, 5),
                Height = 30,
                Cursor = Cursors.Hand
            };

            browseButton.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    locationTextBox.Text = dialog.SelectedPath;
                }
            };

            Grid.SetColumn(locationTextBox, 0);
            Grid.SetColumn(browseButton, 1);

            locationPanel.Children.Add(locationTextBox);
            locationPanel.Children.Add(browseButton);

            // Solution Name
            var solutionLabel = new TextBlock
            {
                Text = "Solution Name:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var solutionTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5),
                Height = 30,
                Text = "MySolution"
            };

            // Framework
            var frameworkLabel = new TextBlock
            {
                Text = "Framework:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var frameworkComboBox = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(10, 5),
                Height = 30
            };

            frameworkComboBox.Items.Add(".NET 8.0");
            frameworkComboBox.Items.Add(".NET 7.0");
            frameworkComboBox.Items.Add(".NET 6.0");
            frameworkComboBox.SelectedIndex = 0;

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var createButton = new Button
            {
                Content = "Create",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 8),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 8),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand
            };

            createButton.Click += (s, e) =>
            {
                var projectName = nameTextBox.Text.Trim();
                var location = locationTextBox.Text.Trim();
                var solutionName = solutionTextBox.Text.Trim();
                var template = templateComboBox.SelectedItem.ToString();
                var framework = frameworkComboBox.SelectedItem.ToString();

                if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(location))
                {
                    MessageBox.Show("Please fill in all required fields.", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CreateProject(projectName, location, solutionName, template, framework);
                window.Close();
            };

            cancelButton.Click += (s, e) => window.Close();

            buttonPanel.Children.Add(createButton);
            buttonPanel.Children.Add(cancelButton);

            // Add controls to grid
            Grid.SetRow(templateLabel, 0);
            Grid.SetRow(templateComboBox, 1);
            Grid.SetRow(nameLabel, 2);
            Grid.SetRow(nameTextBox, 3);
            Grid.SetRow(locationLabel, 4);
            Grid.SetRow(locationPanel, 5);
            Grid.SetRow(solutionLabel, 6);
            Grid.SetRow(solutionTextBox, 7);
            Grid.SetRow(frameworkLabel, 8);
            Grid.SetRow(frameworkComboBox, 9);
            Grid.SetRow(buttonPanel, 10);

            grid.Children.Add(templateLabel);
            grid.Children.Add(templateComboBox);
            grid.Children.Add(nameLabel);
            grid.Children.Add(nameTextBox);
            grid.Children.Add(locationLabel);
            grid.Children.Add(locationPanel);
            grid.Children.Add(solutionLabel);
            grid.Children.Add(solutionTextBox);
            grid.Children.Add(frameworkLabel);
            grid.Children.Add(frameworkComboBox);
            grid.Children.Add(buttonPanel);

            window.Content = grid;
            window.Show();
        }

        private void CreateProject(string projectName, string location, string solutionName, string template, string framework)
        {
            try
            {
                var projectPath = Path.Combine(location, projectName);
                var solutionPath = Path.Combine(location, solutionName);

                // Create directories
                Directory.CreateDirectory(projectPath);
                Directory.CreateDirectory(solutionPath);

                // Create project file
                var projectFile = CreateProjectFile(projectName, template, framework);
                File.WriteAllText(Path.Combine(projectPath, $"{projectName}.csproj"), projectFile);

                // Create solution file
                var solutionFile = CreateSolutionFile(solutionName, projectName);
                File.WriteAllText(Path.Combine(solutionPath, $"{solutionName}.sln"), solutionFile);

                // Create template files
                CreateTemplateFiles(projectPath, projectName, template);

                // Load the project
                LoadProject(Path.Combine(solutionPath, $"{solutionName}.sln"));

                MessageBox.Show($"Project '{projectName}' created successfully!", "Success", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating project: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string CreateProjectFile(string projectName, string template, string framework)
        {
            var frameworkVersion = framework.Replace(".NET ", "").Replace(".", "");
            
            return template switch
            {
                "WPF Application" => $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net{frameworkVersion}-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyTitle>{projectName}</AssemblyTitle>
    <AssemblyDescription>WPF Application created with OhGees IDE</AssemblyDescription>
  </PropertyGroup>

</Project>",
                "Console Application" => $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net{frameworkVersion}</TargetFramework>
    <AssemblyTitle>{projectName}</AssemblyTitle>
    <AssemblyDescription>Console Application created with OhGees IDE</AssemblyDescription>
  </PropertyGroup>

</Project>",
                "Class Library" => $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net{frameworkVersion}</TargetFramework>
    <AssemblyTitle>{projectName}</AssemblyTitle>
    <AssemblyDescription>Class Library created with OhGees IDE</AssemblyDescription>
  </PropertyGroup>

</Project>",
                _ => $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net{frameworkVersion}</TargetFramework>
    <AssemblyTitle>{projectName}</AssemblyTitle>
    <AssemblyDescription>Project created with OhGees IDE</AssemblyDescription>
  </PropertyGroup>

</Project>"
            };
        }

        private string CreateSolutionFile(string solutionName, string projectName)
        {
            return $@"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}"") = ""{projectName}"", ""{projectName}\\{projectName}.csproj"", ""{{PROJECT_GUID}}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{{PROJECT_GUID}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{{PROJECT_GUID}}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{{PROJECT_GUID}}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{{PROJECT_GUID}}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal";
        }

        private void CreateTemplateFiles(string projectPath, string projectName, string template)
        {
            switch (template)
            {
                case "WPF Application":
                    // Create App.xaml
                    var appXaml = @"<Application x:Class=""{PROJECT_NAME}.App""
             xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
             xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
             StartupUri=""MainWindow.xaml"">
    <Application.Resources>
         
    </Application.Resources>
</Application>".Replace("{PROJECT_NAME}", projectName);

                    File.WriteAllText(Path.Combine(projectPath, "App.xaml"), appXaml);

                    // Create App.xaml.cs
                    var appXamlCs = @"using System.Windows;

namespace {PROJECT_NAME}
{
    public partial class App : Application
    {
    }
}".Replace("{PROJECT_NAME}", projectName);

                    File.WriteAllText(Path.Combine(projectPath, "App.xaml.cs"), appXamlCs);

                    // Create MainWindow.xaml
                    var mainWindowXaml = @"<Window x:Class=""{PROJECT_NAME}.MainWindow""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""MainWindow"" Height=""450"" Width=""800"">
    <Grid>
        <TextBlock Text=""Hello, OhGees IDE!"" 
                   HorizontalAlignment=""Center"" 
                   VerticalAlignment=""Center"" 
                   FontSize=""24""/>
    </Grid>
</Window>".Replace("{PROJECT_NAME}", projectName);

                    File.WriteAllText(Path.Combine(projectPath, "MainWindow.xaml"), mainWindowXaml);

                    // Create MainWindow.xaml.cs
                    var mainWindowXamlCs = @"using System.Windows;

namespace {PROJECT_NAME}
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}".Replace("{PROJECT_NAME}", projectName);

                    File.WriteAllText(Path.Combine(projectPath, "MainWindow.xaml.cs"), mainWindowXamlCs);
                    break;

                case "Console Application":
                    var programCs = @"using System;

namespace {PROJECT_NAME}
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello, OhGees IDE!"");
            Console.WriteLine(""Press any key to exit..."");
            Console.ReadKey();
        }
    }
}".Replace("{PROJECT_NAME}", projectName);

                    File.WriteAllText(Path.Combine(projectPath, "Program.cs"), programCs);
                    break;

                case "Class Library":
                    var class1Cs = @"using System;

namespace {PROJECT_NAME}
{
    public class Class1
    {
        public string GetMessage()
        {
            return ""Hello from OhGees IDE!"";
        }
    }
}".Replace("{PROJECT_NAME}", projectName);

                    File.WriteAllText(Path.Combine(projectPath, "Class1.cs"), class1Cs);
                    break;
            }
        }

        public void OpenProject()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Solution Files (*.sln)|*.sln|Project Files (*.csproj)|*.csproj|All Files (*.*)|*.*",
                Title = "Open Project"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadProject(openFileDialog.FileName);
            }
        }

        public void LoadProject(string projectPath)
        {
            try
            {
                _currentProjectPath = projectPath;
                _currentProject = new Project
                {
                    Name = Path.GetFileNameWithoutExtension(projectPath),
                    Path = projectPath,
                    Type = Path.GetExtension(projectPath) == ".sln" ? "Solution" : "Project"
                };

                LoadProjectStructure();
                UpdateSolutionExplorer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading project: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProjectStructure()
        {
            if (_currentProject == null) return;

            _currentProject.Files.Clear();
            _currentProject.Folders.Clear();

            if (_currentProject.Type == "Solution")
            {
                LoadSolutionStructure();
            }
            else
            {
                LoadProjectFiles(_currentProject.Path);
            }
        }

        private void LoadSolutionStructure()
        {
            var solutionDir = Path.GetDirectoryName(_currentProject.Path);
            var projectFiles = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories);

            foreach (var projectFile in projectFiles)
            {
                var projectName = Path.GetFileNameWithoutExtension(projectFile);
                var projectDir = Path.GetDirectoryName(projectFile);

                var project = new Project
                {
                    Name = projectName,
                    Path = projectFile,
                    Type = "Project"
                };

                LoadProjectFiles(projectFile);
                _currentProject.Folders.Add(project);
            }
        }

        private void LoadProjectFiles(string projectPath)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            if (!Directory.Exists(projectDir)) return;

            var files = Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".csproj") && !f.EndsWith(".sln"))
                .ToList();

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(projectDir, file);
                var fileName = Path.GetFileName(file);

                _currentProject.Files.Add(new ProjectFile
                {
                    Name = fileName,
                    Path = file,
                    RelativePath = relativePath
                });
            }
        }

        private void UpdateSolutionExplorer()
        {
            _solutionExplorer.Items.Clear();

            if (_currentProject == null)
            {
                var noProjectItem = new TreeViewItem
                {
                    Header = "No project loaded",
                    Foreground = Brushes.Gray
                };
                _solutionExplorer.Items.Add(noProjectItem);
                return;
            }

            var solutionItem = new TreeViewItem
            {
                Header = $"📁 {_currentProject.Name} ({_currentProject.Type})",
                IsExpanded = true
            };

            if (_currentProject.Type == "Solution")
            {
                foreach (var project in _currentProject.Folders)
                {
                    var projectItem = new TreeViewItem
                    {
                        Header = $"📁 {project.Name}",
                        IsExpanded = true
                    };

                    // Add project files
                    var projectFiles = _currentProject.Files.Where(f => f.RelativePath.StartsWith(project.Name)).ToList();
                    foreach (var file in projectFiles)
                    {
                        var fileIcon = GetFileIcon(file.Name);
                        var fileItem = new TreeViewItem
                        {
                            Header = $"{fileIcon} {file.Name}",
                            Tag = file.Path
                        };
                        projectItem.Items.Add(fileItem);
                    }

                    solutionItem.Items.Add(projectItem);
                }
            }
            else
            {
                // Single project
                foreach (var file in _currentProject.Files)
                {
                    var fileIcon = GetFileIcon(file.Name);
                    var fileItem = new TreeViewItem
                    {
                        Header = $"{fileIcon} {file.Name}",
                        Tag = file.Path
                    };
                    solutionItem.Items.Add(fileItem);
                }
            }

            _solutionExplorer.Items.Add(solutionItem);
        }

        private string GetFileIcon(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".cs" => "📄",
                ".xaml" => "🎨",
                ".xml" => "📋",
                ".json" => "📊",
                ".js" => "📜",
                ".html" => "🌐",
                ".css" => "🎨",
                ".png" => "🖼️",
                ".jpg" => "🖼️",
                ".jpeg" => "🖼️",
                ".gif" => "🖼️",
                ".ico" => "🖼️",
                ".txt" => "📝",
                ".md" => "📝",
                ".config" => "⚙️",
                ".sln" => "📦",
                ".csproj" => "📦",
                _ => "📄"
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class Project
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public List<ProjectFile> Files { get; set; } = new List<ProjectFile>();
        public List<Project> Folders { get; set; } = new List<Project>();
    }

    public class ProjectFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string RelativePath { get; set; }
    }

    #endregion
}
