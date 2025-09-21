using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KimiAppNative.IDE
{
    public class FileExplorerManager
    {
        private readonly TreeView _solutionExplorer;
        private readonly IDEMainWindow _ideWindow;
        private Dictionary<string, TreeViewItem> _fileNodes;

        public FileExplorerManager(TreeView solutionExplorer, IDEMainWindow ideWindow)
        {
            _solutionExplorer = solutionExplorer;
            _ideWindow = ideWindow;
            _fileNodes = new Dictionary<string, TreeViewItem>();
            
            SetupFileExplorer();
        }

        private void SetupFileExplorer()
        {
            _solutionExplorer.Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            _solutionExplorer.Foreground = Brushes.White;
            _solutionExplorer.BorderThickness = new Thickness(0);
            
            // Add context menu
            var contextMenu = new ContextMenu();
            
            var newFileMenuItem = new MenuItem { Header = "New File" };
            newFileMenuItem.Click += NewFile_Click;
            
            var newFolderMenuItem = new MenuItem { Header = "New Folder" };
            newFolderMenuItem.Click += NewFolder_Click;
            
            var openFileMenuItem = new MenuItem { Header = "Open File" };
            openFileMenuItem.Click += OpenFile_Click;
            
            var deleteMenuItem = new MenuItem { Header = "Delete" };
            deleteMenuItem.Click += Delete_Click;
            
            var renameMenuItem = new MenuItem { Header = "Rename" };
            renameMenuItem.Click += Rename_Click;
            
            var refreshMenuItem = new MenuItem { Header = "Refresh" };
            refreshMenuItem.Click += Refresh_Click;
            
            contextMenu.Items.Add(newFileMenuItem);
            contextMenu.Items.Add(newFolderMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(openFileMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(renameMenuItem);
            contextMenu.Items.Add(deleteMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(refreshMenuItem);
            
            _solutionExplorer.ContextMenu = contextMenu;
        }

        public void OpenFile(string fileName)
        {
            // This would be called from the solution explorer selection
            // The actual file opening is handled by the CodeEditorManager
            System.Diagnostics.Debug.WriteLine($"Opening file: {fileName}");
        }

        public void RefreshExplorer()
        {
            // Refresh the solution explorer
            // This would reload the project structure
        }

        #region Context Menu Handlers

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = _solutionExplorer.SelectedItem as TreeViewItem;
            if (selectedItem != null)
            {
                CreateNewFile(selectedItem);
            }
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = _solutionExplorer.SelectedItem as TreeViewItem;
            if (selectedItem != null)
            {
                CreateNewFolder(selectedItem);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = _solutionExplorer.SelectedItem as TreeViewItem;
            if (selectedItem?.Tag != null)
            {
                var filePath = selectedItem.Tag.ToString();
                if (File.Exists(filePath))
                {
                    // Open file in editor
                    System.Diagnostics.Debug.WriteLine($"Opening file: {filePath}");
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = _solutionExplorer.SelectedItem as TreeViewItem;
            if (selectedItem?.Tag != null)
            {
                var path = selectedItem.Tag.ToString();
                var isFile = File.Exists(path);
                var isDirectory = Directory.Exists(path);
                
                var itemType = isFile ? "file" : "folder";
                var result = MessageBox.Show(
                    $"Are you sure you want to delete this {itemType}?\n\n{path}",
                    "Delete Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (isFile)
                        {
                            File.Delete(path);
                        }
                        else if (isDirectory)
                        {
                            Directory.Delete(path, true);
                        }
                        
                        // Remove from tree
                        var parent = selectedItem.Parent as TreeViewItem;
                        parent?.Items.Remove(selectedItem);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting {itemType}: {ex.Message}", "Error", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = _solutionExplorer.SelectedItem as TreeViewItem;
            if (selectedItem?.Tag != null)
            {
                var path = selectedItem.Tag.ToString();
                var isFile = File.Exists(path);
                var isDirectory = Directory.Exists(path);
                
                var currentName = Path.GetFileName(path);
                var newName = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter new name for {currentName}:",
                    "Rename",
                    currentName);

                if (!string.IsNullOrEmpty(newName) && newName != currentName)
                {
                    try
                    {
                        var newPath = Path.Combine(Path.GetDirectoryName(path), newName);
                        
                        if (isFile)
                        {
                            File.Move(path, newPath);
                        }
                        else if (isDirectory)
                        {
                            Directory.Move(path, newPath);
                        }
                        
                        // Update tree item
                        selectedItem.Tag = newPath;
                        selectedItem.Header = GetFileIcon(newName) + " " + newName;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming: {ex.Message}", "Error", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshExplorer();
        }

        #endregion

        #region File Operations

        private void CreateNewFile(TreeViewItem parentItem)
        {
            var fileName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file name:",
                "New File",
                "NewFile.cs");

            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    var parentPath = GetItemPath(parentItem);
                    var filePath = Path.Combine(parentPath, fileName);
                    
                    // Create file
                    File.WriteAllText(filePath, GetDefaultFileContent(fileName));
                    
                    // Add to tree
                    var fileItem = new TreeViewItem
                    {
                        Header = GetFileIcon(fileName) + " " + fileName,
                        Tag = filePath
                    };
                    
                    parentItem.Items.Add(fileItem);
                    parentItem.IsExpanded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating file: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CreateNewFolder(TreeViewItem parentItem)
        {
            var folderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter folder name:",
                "New Folder",
                "NewFolder");

            if (!string.IsNullOrEmpty(folderName))
            {
                try
                {
                    var parentPath = GetItemPath(parentItem);
                    var folderPath = Path.Combine(parentPath, folderName);
                    
                    // Create folder
                    Directory.CreateDirectory(folderPath);
                    
                    // Add to tree
                    var folderItem = new TreeViewItem
                    {
                        Header = "📁 " + folderName,
                        Tag = folderPath
                    };
                    
                    parentItem.Items.Add(folderItem);
                    parentItem.IsExpanded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating folder: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GetItemPath(TreeViewItem item)
        {
            if (item.Tag != null)
            {
                var path = item.Tag.ToString();
                if (File.Exists(path))
                {
                    return Path.GetDirectoryName(path);
                }
                else if (Directory.Exists(path))
                {
                    return path;
                }
            }
            
            // Fallback to current directory
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private string GetDefaultFileContent(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            
            return extension switch
            {
                ".cs" => @"using System;

namespace MyProject
{
    class " + Path.GetFileNameWithoutExtension(fileName) + @"
    {
        // TODO: Implement class functionality
    }
}",
                ".xaml" => @"<Window x:Class=""MyProject." + Path.GetFileNameWithoutExtension(fileName) + @"""
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""" + Path.GetFileNameWithoutExtension(fileName) + @""" Height=""450"" Width=""800"">
    <Grid>
        
    </Grid>
</Window>",
                ".json" => @"{
    ""name"": """ + Path.GetFileNameWithoutExtension(fileName) + @""",
    ""version"": ""1.0.0"",
    ""description"": """"
}",
                ".html" => @"<!DOCTYPE html>
<html>
<head>
    <title>" + Path.GetFileNameWithoutExtension(fileName) + @"</title>
</head>
<body>
    
</body>
</html>",
                ".css" => @"/* " + fileName + @" */

body {
    margin: 0;
    padding: 0;
}",
                ".js" => @"// " + fileName + @"

function main() {
    console.log('Hello, World!');
}

main();",
                ".md" => @"# " + Path.GetFileNameWithoutExtension(fileName) + @"

## Description

TODO: Add description

## Usage

TODO: Add usage instructions",
                _ => "// " + fileName + "\n\n// TODO: Add content"
            };
        }

        #endregion

        #region Helper Methods

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
                ".exe" => "⚙️",
                ".dll" => "📦",
                _ => "📄"
            };
        }

        #endregion
    }
}
