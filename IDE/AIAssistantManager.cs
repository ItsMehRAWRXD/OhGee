using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KimiAppNative.IDE
{
    public class AIAssistantManager
    {
        private readonly IDEMainWindow _ideWindow;
        private readonly HttpClient _httpClient;
        private readonly List<AIModel> _availableModels;
        private AIModel _currentModel;
        
        // Events for IDE integration
        public event Action<string, int> OnCodeSuggestion;
        public event Action<string, string> OnCodeGeneration;
        public event Action<string> OnCodeExplanation;
        public event Action<List<BugReport>> OnBugDetection;
        public event Action<string, string> OnRefactoringSuggestion;

        public AIAssistantManager(IDEMainWindow ideWindow)
        {
            _ideWindow = ideWindow;
            _httpClient = new HttpClient();
            _availableModels = InitializeAIModels();
            _currentModel = _availableModels.FirstOrDefault(m => m.Name == "Kimi") ?? _availableModels.First();
        }

        private List<AIModel> InitializeAIModels()
        {
            return new List<AIModel>
            {
                new AIModel
                {
                    Name = "Kimi",
                    Provider = "Moonshot",
                    Endpoint = "https://api.moonshot.cn/v1/chat/completions",
                    ApiKey = Environment.GetEnvironmentVariable("MOONSHOT_API_KEY"),
                    ModelId = "moonshot-v1-8k",
                    MaxTokens = 8000,
                    Temperature = 0.7f
                },
                new AIModel
                {
                    Name = "ChatGPT",
                    Provider = "OpenAI",
                    Endpoint = "https://api.openai.com/v1/chat/completions",
                    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                    ModelId = "gpt-3.5-turbo",
                    MaxTokens = 4000,
                    Temperature = 0.7f
                },
                new AIModel
                {
                    Name = "DeepSeek",
                    Provider = "DeepSeek",
                    Endpoint = "https://api.deepseek.com/v1/chat/completions",
                    ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
                    ModelId = "deepseek-chat",
                    MaxTokens = 4000,
                    Temperature = 0.7f
                }
            };
        }

        #region Code Analysis and Suggestions

        public async Task AnalyzeCode(string code, int cursorPosition)
        {
            if (string.IsNullOrEmpty(code)) return;

            try
            {
                // Get context around cursor
                var context = GetCodeContext(code, cursorPosition);
                var language = DetectLanguage(code);
                
                // Analyze for potential issues
                await AnalyzeForBugs(code, language);
                
                // Generate suggestions based on context
                await GenerateContextualSuggestions(context, language, cursorPosition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI Analysis Error: {ex.Message}");
            }
        }

        private string GetCodeContext(string code, int cursorPosition)
        {
            var lines = code.Split('\n');
            var currentLineIndex = code.Substring(0, cursorPosition).Split('\n').Length - 1;
            
            // Get 5 lines before and after cursor
            var startLine = Math.Max(0, currentLineIndex - 5);
            var endLine = Math.Min(lines.Length - 1, currentLineIndex + 5);
            
            var contextLines = new List<string>();
            for (int i = startLine; i <= endLine; i++)
            {
                contextLines.Add($"{i + 1}: {lines[i]}");
            }
            
            return string.Join("\n", contextLines);
        }

        private string DetectLanguage(string code)
        {
            // Simple language detection based on keywords and patterns
            if (code.Contains("using System;") || code.Contains("namespace ") || code.Contains("class "))
                return "C#";
            if (code.Contains("import ") || code.Contains("def ") || code.Contains("class "))
                return "Python";
            if (code.Contains("function ") || code.Contains("var ") || code.Contains("const "))
                return "JavaScript";
            if (code.Contains("<Window") || code.Contains("xmlns="))
                return "XAML";
            if (code.Contains("public class") || code.Contains("private "))
                return "Java";
            
            return "Unknown";
        }

        #endregion

        #region AI-Powered Code Completion

        public async Task RequestCodeCompletion(string code, int cursorPosition)
        {
            try
            {
                var context = GetCodeContext(code, cursorPosition);
                var language = DetectLanguage(code);
                
                var prompt = CreateCompletionPrompt(context, language, cursorPosition);
                var completion = await CallAI(prompt, "code_completion");
                
                if (!string.IsNullOrEmpty(completion))
                {
                    OnCodeSuggestion?.Invoke(completion, cursorPosition);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Code Completion Error: {ex.Message}");
            }
        }

        private string CreateCompletionPrompt(string context, string language, int cursorPosition)
        {
            return $@"You are an expert {language} developer. Based on the following code context, suggest the next line or completion:

Context:
{context}

Please provide a concise code completion that follows {language} best practices. Only return the code, no explanations.";
        }

        #endregion

        #region AI-Powered Code Generation

        public async Task GenerateCode(string description, string language, string context = "")
        {
            try
            {
                var prompt = CreateGenerationPrompt(description, language, context);
                var generatedCode = await CallAI(prompt, "code_generation");
                
                if (!string.IsNullOrEmpty(generatedCode))
                {
                    OnCodeGeneration?.Invoke(generatedCode, $"Generated {description}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Code Generation Error: {ex.Message}");
            }
        }

        private string CreateGenerationPrompt(string description, string language, string context)
        {
            return $@"You are an expert {language} developer. Generate code for the following request:

Request: {description}
Language: {language}
Context: {context}

Please provide clean, well-commented code that follows {language} best practices. Include proper error handling and documentation.";
        }

        #endregion

        #region AI-Powered Code Explanation

        public async Task ExplainCode(string code, int cursorPosition)
        {
            try
            {
                var context = GetCodeContext(code, cursorPosition);
                var language = DetectLanguage(code);
                
                var prompt = CreateExplanationPrompt(context, language);
                var explanation = await CallAI(prompt, "code_explanation");
                
                if (!string.IsNullOrEmpty(explanation))
                {
                    OnCodeExplanation?.Invoke(explanation);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Code Explanation Error: {ex.Message}");
            }
        }

        private string CreateExplanationPrompt(string context, string language)
        {
            return $@"You are an expert {language} developer and teacher. Explain the following code in detail:

Code:
{context}

Please provide a clear explanation covering:
1. What the code does
2. How it works
3. Key concepts and patterns used
4. Potential improvements or considerations

Make the explanation educational and easy to understand.";
        }

        #endregion

        #region AI-Powered Bug Detection

        public async Task AnalyzeForBugs(string code, string language)
        {
            try
            {
                var prompt = CreateBugDetectionPrompt(code, language);
                var analysis = await CallAI(prompt, "bug_detection");
                
                if (!string.IsNullOrEmpty(analysis))
                {
                    var bugs = ParseBugAnalysis(analysis);
                    OnBugDetection?.Invoke(bugs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bug Detection Error: {ex.Message}");
            }
        }

        private string CreateBugDetectionPrompt(string code, string language)
        {
            return $@"You are an expert {language} code reviewer. Analyze the following code for potential bugs, issues, and improvements:

Code:
{code}

Please identify:
1. Syntax errors
2. Logic errors
3. Performance issues
4. Security vulnerabilities
5. Code quality issues

Format your response as:
SEVERITY: DESCRIPTION (Line X)
Where SEVERITY is Error, Warning, or Info.";
        }

        private List<BugReport> ParseBugAnalysis(string analysis)
        {
            var bugs = new List<BugReport>();
            var lines = analysis.Split('\n');
            
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"(Error|Warning|Info):\s*(.+?)\s*\(Line\s*(\d+)\)");
                if (match.Success)
                {
                    bugs.Add(new BugReport
                    {
                        Severity = match.Groups[1].Value,
                        Description = match.Groups[2].Value,
                        Line = int.Parse(match.Groups[3].Value)
                    });
                }
            }
            
            return bugs;
        }

        #endregion

        #region AI-Powered Refactoring

        public async Task SuggestRefactoring(string code, int cursorPosition)
        {
            try
            {
                var context = GetCodeContext(code, cursorPosition);
                var language = DetectLanguage(code);
                
                var prompt = CreateRefactoringPrompt(context, language);
                var refactoring = await CallAI(prompt, "refactoring");
                
                if (!string.IsNullOrEmpty(refactoring))
                {
                    OnRefactoringSuggestion?.Invoke("AI Refactoring Suggestion", refactoring);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refactoring Error: {ex.Message}");
            }
        }

        private string CreateRefactoringPrompt(string context, string language)
        {
            return $@"You are an expert {language} developer. Refactor the following code to improve:
1. Readability
2. Performance
3. Maintainability
4. Best practices

Original Code:
{context}

Please provide the refactored version with explanations of the improvements made.";
        }

        #endregion

        #region AI Model Management

        public void SwitchModel(string modelName)
        {
            var model = _availableModels.FirstOrDefault(m => m.Name == modelName);
            if (model != null)
            {
                _currentModel = model;
                System.Diagnostics.Debug.WriteLine($"Switched to AI model: {model.Name}");
            }
        }

        public List<string> GetAvailableModels()
        {
            return _availableModels.Select(m => m.Name).ToList();
        }

        public string GetCurrentModel()
        {
            return _currentModel?.Name ?? "None";
        }

        #endregion

        #region AI Assistant Panel

        public void ShowAIAssistantPanel()
        {
            var window = new Window
            {
                Title = "🤖 OhGees AI Assistant",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _ideWindow,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Model selection
            var modelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };

            var modelLabel = new TextBlock
            {
                Text = "AI Model:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var modelComboBox = new ComboBox
            {
                Width = 150,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };

            foreach (var model in _availableModels)
            {
                modelComboBox.Items.Add(model.Name);
            }
            modelComboBox.SelectedItem = _currentModel.Name;
            modelComboBox.SelectionChanged += (s, e) =>
            {
                if (modelComboBox.SelectedItem != null)
                {
                    SwitchModel(modelComboBox.SelectedItem.ToString());
                }
            };

            modelPanel.Children.Add(modelLabel);
            modelPanel.Children.Add(modelComboBox);
            Grid.SetRow(modelPanel, 0);

            // Quick actions
            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 5)
            };

            var explainButton = new Button
            {
                Content = "🔍 Explain Code",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand
            };

            var refactorButton = new Button
            {
                Content = "🔧 Refactor",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand
            };

            var generateButton = new Button
            {
                Content = "⚡ Generate Code",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand
            };

            explainButton.Click += async (s, e) =>
            {
                // Get current code from IDE
                var currentCode = GetCurrentCodeFromIDE();
                await ExplainCode(currentCode, 0);
            };

            refactorButton.Click += async (s, e) =>
            {
                var currentCode = GetCurrentCodeFromIDE();
                await SuggestRefactoring(currentCode, 0);
            };

            generateButton.Click += async (s, e) =>
            {
                var description = Microsoft.VisualBasic.Interaction.InputBox(
                    "Describe the code you want to generate:", "AI Code Generation", "");
                if (!string.IsNullOrEmpty(description))
                {
                    var language = DetectLanguage(GetCurrentCodeFromIDE());
                    await GenerateCode(description, language);
                }
            };

            actionsPanel.Children.Add(explainButton);
            actionsPanel.Children.Add(refactorButton);
            actionsPanel.Children.Add(generateButton);
            Grid.SetRow(actionsPanel, 1);

            // Chat area
            var chatScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 5)
            };

            var chatPanel = new StackPanel
            {
                Name = "ChatPanel"
            };

            // Add welcome message
            var welcomeMessage = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var welcomeText = new TextBlock
            {
                Text = "🤖 Welcome to OhGees AI Assistant!\n\nI can help you with:\n• Code completion and suggestions\n• Code explanation and documentation\n• Bug detection and analysis\n• Code refactoring and optimization\n• Code generation from descriptions\n\nAsk me anything about your code!",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };

            welcomeMessage.Child = welcomeText;
            chatPanel.Children.Add(welcomeMessage);

            chatScrollViewer.Content = chatPanel;
            Grid.SetRow(chatScrollViewer, 2);

            // Input area
            var inputPanel = new Grid();
            inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var inputTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontSize = 12,
                Padding = new Thickness(8),
                Margin = new Thickness(10, 5, 5, 10)
            };

            var sendButton = new Button
            {
                Content = "Send",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 8),
                Margin = new Thickness(5, 5, 10, 10),
                Cursor = Cursors.Hand
            };

            sendButton.Click += async (s, e) =>
            {
                var message = inputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(message))
                {
                    await HandleChatMessage(message, chatPanel);
                    inputTextBox.Clear();
                }
            };

            inputTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
                {
                    sendButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };

            Grid.SetColumn(inputTextBox, 0);
            Grid.SetColumn(sendButton, 1);

            inputPanel.Children.Add(inputTextBox);
            inputPanel.Children.Add(sendButton);
            Grid.SetRow(inputPanel, 3);

            grid.Children.Add(modelPanel);
            grid.Children.Add(actionsPanel);
            grid.Children.Add(chatScrollViewer);
            grid.Children.Add(inputPanel);

            window.Content = grid;
            window.Show();
        }

        private async Task HandleChatMessage(string message, StackPanel chatPanel)
        {
            // Add user message to chat
            AddMessageToChat(chatPanel, message, true);

            // Process message with AI
            try
            {
                var response = await ProcessChatMessage(message);
                AddMessageToChat(chatPanel, response, false);
            }
            catch (Exception ex)
            {
                AddMessageToChat(chatPanel, $"Sorry, I encountered an error: {ex.Message}", false);
            }
        }

        private async Task<string> ProcessChatMessage(string message)
        {
            var prompt = $@"You are OhGees AI Assistant, an expert coding assistant. The user is asking: {message}

Please provide a helpful response. If they're asking about code, provide practical examples and explanations.";
            
            return await CallAI(prompt, "chat");
        }

        private void AddMessageToChat(StackPanel chatPanel, string message, bool isUser)
        {
            var messageBorder = new Border
            {
                Background = new SolidColorBrush(isUser ? Color.FromRgb(0, 122, 204) : Color.FromRgb(45, 45, 48)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(isUser ? 50 : 0, 0, isUser ? 0 : 50, 10),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var messageText = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };

            messageBorder.Child = messageText;
            chatPanel.Children.Add(messageBorder);
        }

        #endregion

        #region Core AI Communication

        private async Task<string> CallAI(string prompt, string context)
        {
            if (string.IsNullOrEmpty(_currentModel.ApiKey))
            {
                return $"API key not configured for {_currentModel.Name}. Please set the environment variable {_currentModel.Provider.ToUpper()}_API_KEY.";
            }

            try
            {
                var requestBody = new
                {
                    model = _currentModel.ModelId,
                    messages = new[]
                    {
                        new { role = "system", content = GetSystemPrompt(context) },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = Math.Min(_currentModel.MaxTokens, 2000),
                    temperature = _currentModel.Temperature
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_currentModel.ApiKey}");

                var response = await _httpClient.PostAsync(_currentModel.Endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("message", out var message) && 
                            message.TryGetProperty("content", out var contentProp))
                        {
                            return contentProp.GetString() ?? "No response generated";
                        }
                    }
                }

                return $"Error: {response.StatusCode} - {responseContent}";
            }
            catch (Exception ex)
            {
                return $"Error calling AI: {ex.Message}";
            }
        }

        private string GetSystemPrompt(string context)
        {
            return context switch
            {
                "code_completion" => "You are an expert code completion assistant. Provide concise, accurate code completions that follow best practices.",
                "code_generation" => "You are an expert code generator. Create clean, well-documented, production-ready code.",
                "code_explanation" => "You are an expert code teacher. Explain code clearly and help developers understand concepts.",
                "bug_detection" => "You are an expert code reviewer. Identify bugs, issues, and improvements in code.",
                "refactoring" => "You are an expert refactoring assistant. Improve code quality, readability, and performance.",
                "chat" => "You are OhGees AI Assistant, a helpful coding assistant. Provide practical, accurate help with programming tasks.",
                _ => "You are a helpful AI coding assistant."
            };
        }

        private string GetCurrentCodeFromIDE()
        {
            // This would get the current code from the IDE's active editor
            // For now, return a placeholder
            return "// Current code from IDE editor";
        }

        #endregion
    }
}
