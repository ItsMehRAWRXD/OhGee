using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KimiAppNative.IDE
{
    public class BuildManager
    {
        private readonly TextBox _outputTextBox;
        private readonly ListBox _errorList;
        private readonly IDEMainWindow _ideWindow;
        private Process _buildProcess;

        public BuildManager(TextBox outputTextBox, ListBox errorList, IDEMainWindow ideWindow)
        {
            _outputTextBox = outputTextBox;
            _errorList = errorList;
            _ideWindow = ideWindow;
        }

        #region Build Operations

        public async Task BuildProject()
        {
            if (!HasValidProject())
            {
                AddOutput("❌ No project loaded. Please open a project first.");
                return;
            }

            try
            {
                AddOutput("🔨 Starting build...");
                ClearErrors();

                var projectPath = GetCurrentProjectPath();
                var buildCommand = $"dotnet build \"{projectPath}\" --verbosity normal";

                await ExecuteBuildCommand(buildCommand);
            }
            catch (Exception ex)
            {
                AddOutput($"❌ Build failed: {ex.Message}");
                AddError("Error", ex.Message, 0);
            }
        }

        public async Task RebuildProject()
        {
            if (!HasValidProject())
            {
                AddOutput("❌ No project loaded. Please open a project first.");
                return;
            }

            try
            {
                AddOutput("🔄 Starting rebuild...");
                ClearErrors();

                var projectPath = GetCurrentProjectPath();
                var buildCommand = $"dotnet clean \"{projectPath}\" && dotnet build \"{projectPath}\" --verbosity normal";

                await ExecuteBuildCommand(buildCommand);
            }
            catch (Exception ex)
            {
                AddOutput($"❌ Rebuild failed: {ex.Message}");
                AddError("Error", ex.Message, 0);
            }
        }

        public async Task CleanProject()
        {
            if (!HasValidProject())
            {
                AddOutput("❌ No project loaded. Please open a project first.");
                return;
            }

            try
            {
                AddOutput("🧹 Cleaning project...");

                var projectPath = GetCurrentProjectPath();
                var cleanCommand = $"dotnet clean \"{projectPath}\"";

                await ExecuteBuildCommand(cleanCommand);
                AddOutput("✅ Project cleaned successfully.");
            }
            catch (Exception ex)
            {
                AddOutput($"❌ Clean failed: {ex.Message}");
                AddError("Error", ex.Message, 0);
            }
        }

        public async Task RunProject()
        {
            if (!HasValidProject())
            {
                AddOutput("❌ No project loaded. Please open a project first.");
                return;
            }

            try
            {
                AddOutput("▶️ Starting application...");

                var projectPath = GetCurrentProjectPath();
                var runCommand = $"dotnet run --project \"{projectPath}\"";

                await ExecuteBuildCommand(runCommand);
            }
            catch (Exception ex)
            {
                AddOutput($"❌ Run failed: {ex.Message}");
                AddError("Error", ex.Message, 0);
            }
        }

        private async Task ExecuteBuildCommand(string command)
        {
            _buildProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(GetCurrentProjectPath())
                }
            };

            _buildProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddOutput(e.Data);
                        ParseBuildOutput(e.Data);
                    });
                }
            };

            _buildProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddOutput($"❌ {e.Data}");
                        ParseBuildError(e.Data);
                    });
                }
            };

            _buildProcess.Start();
            _buildProcess.BeginOutputReadLine();
            _buildProcess.BeginErrorReadLine();

            await _buildProcess.WaitForExitAsync();

            if (_buildProcess.ExitCode == 0)
            {
                AddOutput("✅ Build completed successfully.");
            }
            else
            {
                AddOutput($"❌ Build failed with exit code: {_buildProcess.ExitCode}");
            }
        }

        #endregion

        #region Output Parsing

        private void ParseBuildOutput(string output)
        {
            // Parse build output for warnings and errors
            if (output.Contains("warning"))
            {
                ParseWarning(output);
            }
            else if (output.Contains("error"))
            {
                ParseError(output);
            }
        }

        private void ParseBuildError(string error)
        {
            // Parse error output
            if (error.Contains("error CS"))
            {
                ParseCompilerError(error);
            }
            else
            {
                AddError("Error", error, 0);
            }
        }

        private void ParseWarning(string output)
        {
            // Parse warning format: file(line,column): warning CS####: message
            var match = System.Text.RegularExpressions.Regex.Match(output, 
                @"([^(]+)\((\d+),(\d+)\):\s*warning\s+(CS\d+):\s*(.+)");
            
            if (match.Success)
            {
                var fileName = Path.GetFileName(match.Groups[1].Value);
                var line = int.Parse(match.Groups[2].Value);
                var column = int.Parse(match.Groups[3].Value);
                var code = match.Groups[4].Value;
                var message = match.Groups[5].Value;

                AddError("Warning", $"{code}: {message}", line, fileName);
            }
        }

        private void ParseError(string output)
        {
            // Parse error format: file(line,column): error CS####: message
            var match = System.Text.RegularExpressions.Regex.Match(output, 
                @"([^(]+)\((\d+),(\d+)\):\s*error\s+(CS\d+):\s*(.+)");
            
            if (match.Success)
            {
                var fileName = Path.GetFileName(match.Groups[1].Value);
                var line = int.Parse(match.Groups[2].Value);
                var column = int.Parse(match.Groups[3].Value);
                var code = match.Groups[4].Value;
                var message = match.Groups[5].Value;

                AddError("Error", $"{code}: {message}", line, fileName);
            }
        }

        private void ParseCompilerError(string error)
        {
            // Parse compiler error
            AddError("Error", error, 0);
        }

        #endregion

        #region Output Management

        private void AddOutput(string message)
        {
            _outputTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
            _outputTextBox.ScrollToEnd();
        }

        private void AddError(string severity, string message, int line, string fileName = "")
        {
            var errorItem = new ListBoxItem
            {
                Content = $"{GetSeverityIcon(severity)} {severity}: {message}",
                Foreground = GetSeverityColor(severity),
                Tag = new { FileName = fileName, Line = line, Message = message }
            };

            _errorList.Items.Add(errorItem);
        }

        private void ClearErrors()
        {
            _errorList.Items.Clear();
        }

        private string GetSeverityIcon(string severity)
        {
            return severity switch
            {
                "Error" => "❌",
                "Warning" => "⚠️",
                "Info" => "ℹ️",
                _ => "📝"
            };
        }

        private Brush GetSeverityColor(string severity)
        {
            return severity switch
            {
                "Error" => Brushes.Red,
                "Warning" => Brushes.Orange,
                "Info" => Brushes.Cyan,
                _ => Brushes.White
            };
        }

        #endregion

        #region Helper Methods

        private bool HasValidProject()
        {
            // Check if we have a valid project loaded
            return !string.IsNullOrEmpty(GetCurrentProjectPath());
        }

        private string GetCurrentProjectPath()
        {
            // This would get the current project path from the project manager
            // For now, return a placeholder
            return "C:\\Users\\User\\MyProject\\MyProject.csproj";
        }

        #endregion
    }
}
