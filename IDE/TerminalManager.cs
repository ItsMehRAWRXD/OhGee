using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KimiAppNative.IDE
{
    public class TerminalManager
    {
        private readonly TextBox _outputTextBox;
        private readonly TextBox _inputTextBox;
        private readonly IDEMainWindow _ideWindow;
        private Process _currentProcess;
        private string _currentDirectory;

        public TerminalManager(TextBox outputTextBox, TextBox inputTextBox, IDEMainWindow ideWindow)
        {
            _outputTextBox = outputTextBox;
            _inputTextBox = inputTextBox;
            _ideWindow = ideWindow;
            _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            SetupTerminal();
        }

        private void SetupTerminal()
        {
            // Configure terminal output
            _outputTextBox.FontFamily = new FontFamily("Consolas");
            _outputTextBox.FontSize = 12;
            _outputTextBox.Background = new SolidColorBrush(Color.FromRgb(12, 12, 12));
            _outputTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            _outputTextBox.BorderThickness = new Thickness(0);
            _outputTextBox.IsReadOnly = true;

            // Configure terminal input
            _inputTextBox.FontFamily = new FontFamily("Consolas");
            _inputTextBox.FontSize = 12;
            _inputTextBox.Background = new SolidColorBrush(Color.FromRgb(12, 12, 12));
            _inputTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            _inputTextBox.BorderThickness = new Thickness(0, 1, 0, 0);
            _inputTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));

            // Add welcome message
            AddOutput("OhGees IDE Terminal");
            AddOutput("Type commands below...");
            AddOutput("");
            UpdatePrompt();
        }

        public async Task ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Add command to output
            AddOutput($"{GetPrompt()}{command}");

            try
            {
                // Handle special commands
                if (await HandleSpecialCommand(command))
                    return;

                // Execute system command
                await ExecuteSystemCommand(command);
            }
            catch (Exception ex)
            {
                AddOutput($"Error: {ex.Message}");
            }

            UpdatePrompt();
        }

        private async Task<bool> HandleSpecialCommand(string command)
        {
            var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "clear":
                case "cls":
                    ClearOutput();
                    return true;

                case "cd":
                    if (parts.Length > 1)
                    {
                        ChangeDirectory(parts[1]);
                    }
                    else
                    {
                        AddOutput(_currentDirectory);
                    }
                    return true;

                case "pwd":
                    AddOutput(_currentDirectory);
                    return true;

                case "ls":
                case "dir":
                    ListDirectory();
                    return true;

                case "help":
                    ShowHelp();
                    return true;

                case "dotnet":
                    await ExecuteDotNetCommand(command);
                    return true;

                case "git":
                    await ExecuteGitCommand(command);
                    return true;

                case "exit":
                    _ideWindow.Close();
                    return true;

                default:
                    return false;
            }
        }

        private void ChangeDirectory(string path)
        {
            try
            {
                if (Path.IsPathRooted(path))
                {
                    _currentDirectory = path;
                }
                else
                {
                    _currentDirectory = Path.Combine(_currentDirectory, path);
                }

                if (!Directory.Exists(_currentDirectory))
                {
                    AddOutput($"Directory not found: {_currentDirectory}");
                    _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }
            catch (Exception ex)
            {
                AddOutput($"Error changing directory: {ex.Message}");
            }
        }

        private void ListDirectory()
        {
            try
            {
                var directories = Directory.GetDirectories(_currentDirectory);
                var files = Directory.GetFiles(_currentDirectory);

                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    AddOutput($"📁 {dirName}");
                }

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var extension = Path.GetExtension(file).ToLower();
                    var icon = GetFileIcon(extension);
                    AddOutput($"{icon} {fileName}");
                }
            }
            catch (Exception ex)
            {
                AddOutput($"Error listing directory: {ex.Message}");
            }
        }

        private string GetFileIcon(string extension)
        {
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

        private void ShowHelp()
        {
            AddOutput("OhGees IDE Terminal Commands:");
            AddOutput("  clear/cls     - Clear terminal output");
            AddOutput("  cd <path>     - Change directory");
            AddOutput("  pwd           - Show current directory");
            AddOutput("  ls/dir        - List directory contents");
            AddOutput("  dotnet <cmd>  - Execute .NET CLI commands");
            AddOutput("  git <cmd>     - Execute Git commands");
            AddOutput("  help          - Show this help");
            AddOutput("  exit          - Exit the IDE");
            AddOutput("");
            AddOutput("You can also run any system command or executable.");
        }

        private async Task ExecuteDotNetCommand(string command)
        {
            var dotnetCommand = command.Substring(6).Trim(); // Remove "dotnet " prefix
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = dotnetCommand,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _currentDirectory
                }
            };

            await ExecuteProcess(process);
        }

        private async Task ExecuteGitCommand(string command)
        {
            var gitCommand = command.Substring(4).Trim(); // Remove "git " prefix
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = gitCommand,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _currentDirectory
                }
            };

            await ExecuteProcess(process);
        }

        private async Task ExecuteSystemCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _currentDirectory
                }
            };

            await ExecuteProcess(process);
        }

        private async Task ExecuteProcess(Process process)
        {
            _currentProcess = process;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddOutput(e.Data);
                    });
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddOutput($"❌ {e.Data}");
                    });
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            _currentProcess = null;
        }

        private void AddOutput(string text)
        {
            _outputTextBox.AppendText($"{text}\n");
            _outputTextBox.ScrollToEnd();
        }

        private void ClearOutput()
        {
            _outputTextBox.Clear();
            AddOutput("OhGees IDE Terminal");
            AddOutput("Type commands below...");
            AddOutput("");
        }

        private void UpdatePrompt()
        {
            var prompt = GetPrompt();
            _inputTextBox.Text = prompt;
            _inputTextBox.SelectionStart = prompt.Length;
        }

        private string GetPrompt()
        {
            var drive = Path.GetPathRoot(_currentDirectory);
            var relativePath = Path.GetRelativePath(drive, _currentDirectory);
            return $"PS {_currentDirectory}> ";
        }

        public void StopCurrentProcess()
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                try
                {
                    _currentProcess.Kill();
                    AddOutput("Process terminated.");
                }
                catch (Exception ex)
                {
                    AddOutput($"Error stopping process: {ex.Message}");
                }
            }
        }

        public void SetWorkingDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                _currentDirectory = directory;
                UpdatePrompt();
            }
        }

        public string GetWorkingDirectory()
        {
            return _currentDirectory;
        }
    }
}
