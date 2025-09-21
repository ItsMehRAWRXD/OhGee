using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using Markdig;

namespace KimiAppNative
{
    public partial class ChatWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly DispatcherTimer _typingTimer;
        private readonly ObservableCollection<ChatMessage> _messages;
        private readonly List<AIModel> _availableModels;
        private AIModel _currentModel;
        private bool _isTyping;

        public ChatWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _messages = new ObservableCollection<ChatMessage>();
            _availableModels = InitializeModels();
            _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _typingTimer.Tick += TypingTimer_Tick;
            
            InitializeUI();
            LoadWelcomeMessage();
        }

        private void InitializeUI()
        {
            // Populate model combo box
            ModelComboBox.ItemsSource = _availableModels;
            ModelComboBox.DisplayMemberPath = "DisplayName";
            ModelComboBox.SelectedItem = _availableModels.FirstOrDefault(m => m.Name == "localhost");
            
            // Enable window dragging
            this.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };
        }

        private List<AIModel> InitializeModels()
        {
            return new List<AIModel>
            {
                new AIModel
                {
                    Name = "localhost",
                    DisplayName = "🏠 Localhost Panel (RawrZ)",
                    ApiUrl = "http://localhost:8080/api/ai/chat",
                    RequiresApiKey = false,
                    ApiKeyEnvVar = ""
                },
                new AIModel
                {
                    Name = "openai",
                    DisplayName = "🤖 ChatGPT (OpenAI)",
                    ApiUrl = "https://api.openai.com/v1/chat/completions",
                    RequiresApiKey = true,
                    ApiKeyEnvVar = "OPENAI_API_KEY"
                },
                new AIModel
                {
                    Name = "kimi",
                    DisplayName = "🧠 Kimi (Moonshot)",
                    ApiUrl = "https://api.moonshot.cn/v1/chat/completions",
                    RequiresApiKey = true,
                    ApiKeyEnvVar = "MOONSHOT_API_KEY"
                },
                new AIModel
                {
                    Name = "deepseek",
                    DisplayName = "🔍 DeepSeek V3",
                    ApiUrl = "https://api.deepseek.com/v1/chat/completions",
                    RequiresApiKey = true,
                    ApiKeyEnvVar = "DEEPSEEK_API_KEY"
                }
            };
        }

        private void LoadWelcomeMessage()
        {
            var welcomeMessage = new ChatMessage
            {
                IsUser = false,
                Content = "👋 Welcome to OhGee - AI Assistant Hub!\n\n" +
                         "**Available Models:**\n" +
                         "• 🏠 **Localhost Panel** - Connect to your RawrZ server (default)\n" +
                         "• 🤖 **ChatGPT** - OpenAI's GPT models\n" +
                         "• 🧠 **Kimi** - Moonshot AI models\n" +
                         "• 🔍 **DeepSeek** - Advanced reasoning models\n\n" +
                         "**Features:**\n" +
                         "• Real-time streaming responses\n" +
                         "• System tray integration\n" +
                         "• Global hotkeys for quick access\n" +
                         "• Native Windows performance\n\n" +
                         "Start by selecting a model and asking your question!",
                Timestamp = DateTime.Now
            };
            
            AddMessageToUI(welcomeMessage);
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelComboBox.SelectedItem is AIModel selectedModel)
            {
                _currentModel = selectedModel;
                UpdateStatus($"Selected: {selectedModel.DisplayName}");
                
                if (selectedModel.RequiresApiKey)
                {
                    var apiKey = Environment.GetEnvironmentVariable(selectedModel.ApiKeyEnvVar);
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        UpdateStatus($"⚠️ API key required for {selectedModel.DisplayName}. Set {selectedModel.ApiKeyEnvVar} environment variable.");
                    }
                    else
                    {
                        UpdateStatus($"✅ {selectedModel.DisplayName} ready");
                    }
                }
                else if (selectedModel.Name == "localhost")
                {
                    UpdateStatus($"🏠 {selectedModel.DisplayName} - Make sure RawrZ server is running on port 8080");
                }
                else
                {
                    UpdateStatus($"✅ {selectedModel.DisplayName} ready");
                }
            }
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(MessageTextBox.Text.Trim());
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Allow new line
                    return;
                }
                else
                {
                    e.Handled = true;
                    _ = SendMessage();
                }
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            _ = SendMessage();
        }

        private async Task SendMessage()
        {
            var messageText = MessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(messageText) || _currentModel == null)
                return;

            // Add user message
            var userMessage = new ChatMessage
            {
                IsUser = true,
                Content = messageText,
                Timestamp = DateTime.Now
            };
            AddMessageToUI(userMessage);

            // Clear input
            MessageTextBox.Clear();
            SendButton.IsEnabled = false;

            // Add AI message placeholder
            var aiMessage = new ChatMessage
            {
                IsUser = false,
                Content = "",
                Timestamp = DateTime.Now,
                IsStreaming = true
            };
            AddMessageToUI(aiMessage);

            // Get AI response
            try
            {
                await GetAIResponse(messageText, aiMessage);
            }
            catch (Exception ex)
            {
                aiMessage.Content = $"❌ Error: {ex.Message}";
                aiMessage.IsStreaming = false;
                UpdateMessageUI(aiMessage);
            }
        }

        private async Task GetAIResponse(string userMessage, ChatMessage aiMessage)
        {
            if (_currentModel.Name == "localhost")
            {
                await GetLocalhostResponse(userMessage, aiMessage);
                return;
            }

            var apiKey = Environment.GetEnvironmentVariable(_currentModel.ApiKeyEnvVar);
            if (string.IsNullOrEmpty(apiKey))
            {
                aiMessage.Content = $"❌ API key not found. Please set the {_currentModel.ApiKeyEnvVar} environment variable.";
                aiMessage.IsStreaming = false;
                UpdateMessageUI(aiMessage);
                return;
            }

            try
            {
                var requestBody = new
                {
                    model = GetModelName(_currentModel.Name),
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    },
                    stream = true,
                    max_tokens = 2000,
                    temperature = 0.7
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await _httpClient.PostAsync(_currentModel.ApiUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    aiMessage.Content = $"❌ API Error: {response.StatusCode} - {errorContent}";
                    aiMessage.IsStreaming = false;
                    UpdateMessageUI(aiMessage);
                    return;
                }

                await ProcessStreamingResponse(response, aiMessage);
            }
            catch (Exception ex)
            {
                aiMessage.Content = $"❌ Request failed: {ex.Message}";
                aiMessage.IsStreaming = false;
                UpdateMessageUI(aiMessage);
            }
        }

        private async Task ProcessStreamingResponse(HttpResponseMessage response, ChatMessage aiMessage)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[1024];
            var accumulatedContent = new StringBuilder();

            while (stream.CanRead)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var lines = chunk.Split('\n');

                foreach (var line in lines)
                {
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6).Trim();
                        if (data == "[DONE]")
                        {
                            aiMessage.IsStreaming = false;
                            UpdateMessageUI(aiMessage);
                            return;
                        }

                        try
                        {
                            var jsonResponse = JsonConvert.DeserializeObject<dynamic>(data);
                            var content = jsonResponse?.choices?[0]?.delta?.content;
                            if (content != null)
                            {
                                accumulatedContent.Append(content.ToString());
                                aiMessage.Content = accumulatedContent.ToString();
                                UpdateMessageUI(aiMessage);
                            }
                        }
                        catch
                        {
                            // Ignore malformed JSON
                        }
                    }
                }
            }

            aiMessage.IsStreaming = false;
            UpdateMessageUI(aiMessage);
        }

    private async Task GetLocalhostResponse(string userMessage, ChatMessage aiMessage)
    {
        try
        {
            // First try to connect to localhost panel
            var healthResponse = await _httpClient.GetAsync("http://localhost:8080/health");
            if (!healthResponse.IsSuccessStatusCode)
            {
                aiMessage.Content = "❌ Localhost panel is not running. Please start the RawrZ server on port 8080.\n\nTo start the real bot server, run: node server-real-bots.js";
                aiMessage.IsStreaming = false;
                UpdateMessageUI(aiMessage);
                return;
            }

            // Check if this is a bot-related query
            if (userMessage.ToLower().Contains("bot") || userMessage.ToLower().Contains("status") || userMessage.ToLower().Contains("panel"))
            {
                // Get real bot status
                var statusResponse = await _httpClient.GetAsync("http://localhost:8080/api/botnet/status");
                if (statusResponse.IsSuccessStatusCode)
                {
                    var statusContent = await statusResponse.Content.ReadAsStringAsync();
                    var statusResult = JsonConvert.DeserializeObject<dynamic>(statusContent);
                    
                    if (statusResult?.status == 200)
                    {
                        var data = statusResult.data;
                        aiMessage.Content = $"🤖 **Real Bot Status**\n\n" +
                                          $"📊 **Active Bots**: {data?.onlineBots}\n" +
                                          $"📈 **Total Bots**: {data?.totalBots}\n" +
                                          $"📝 **Total Logs**: {data?.totalLogs}\n" +
                                          $"⚡ **Active Tasks**: {data?.activeTasks}\n\n" +
                                          $"🔧 **Real-time Stats**:\n" +
                                          $"• Commands Executed: {data?.realTimeStats?.commandsExecuted}\n" +
                                          $"• Files Transferred: {data?.realTimeStats?.filesTransferred}\n" +
                                          $"• Screenshots Taken: {data?.realTimeStats?.screenshotsTaken}\n" +
                                          $"• Keylogs Collected: {data?.realTimeStats?.keylogsCollected}\n\n" +
                                          $"✅ Connected to real bot implementations!";
                    }
                    else
                    {
                        aiMessage.Content = "❌ Failed to get bot status from real server.";
                    }
                }
                else
                {
                    aiMessage.Content = "❌ Real bot server not responding. Make sure server-real-bots.js is running.";
                }
            }
            else
            {
                // Try to use the CLI endpoint for general commands
                var requestBody = new
                {
                    command = "help",
                    args = new[] { userMessage }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("http://localhost:8080/cli", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    
                    if (result?.success == true)
                    {
                        aiMessage.Content = $"🔧 **RawrZ CLI Response**\n\n{result.result?.ToString() ?? "Command executed successfully."}\n\n" +
                                          $"💡 **Tip**: Ask about 'bot status' to see real bot network information!";
                    }
                    else
                    {
                        aiMessage.Content = "❌ Localhost panel responded but couldn't process the request.";
                    }
                }
                else
                {
                    aiMessage.Content = $"❌ Localhost panel error: {response.StatusCode}";
                }
            }
        }
        catch (Exception ex)
        {
            aiMessage.Content = $"❌ Failed to connect to localhost panel: {ex.Message}\n\n" +
                              $"**To start the real bot server:**\n" +
                              $"1. Run: `node server-real-bots.js`\n" +
                              $"2. This connects to actual bot implementations\n" +
                              $"3. Access panel at: http://localhost:8080/panel";
        }

        aiMessage.IsStreaming = false;
        UpdateMessageUI(aiMessage);
    }

        private string GetModelName(string modelKey)
        {
            return modelKey switch
            {
                "openai" => "gpt-3.5-turbo",
                "kimi" => "moonshot-v1-8k",
                "deepseek" => "deepseek-chat",
                _ => "gpt-3.5-turbo"
            };
        }

        private void AddMessageToUI(ChatMessage message)
        {
            _messages.Add(message);
            var messageControl = CreateMessageControl(message);
            ChatPanel.Children.Add(messageControl);
            ScrollToBottom();
        }

        private void UpdateMessageUI(ChatMessage message)
        {
            var index = _messages.IndexOf(message);
            if (index >= 0)
            {
                ChatPanel.Children.RemoveAt(index);
                var messageControl = CreateMessageControl(message);
                ChatPanel.Children.Insert(index, messageControl);
                ScrollToBottom();
            }
        }

        private UIElement CreateMessageControl(ChatMessage message)
        {
            var border = new Border
            {
                Style = message.IsUser ? 
                    (Style)FindResource("UserMessageStyle") : 
                    (Style)FindResource("AIMessageStyle")
            };

            var stackPanel = new StackPanel();

            // Message content
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = message.IsUser ? Brushes.White : Brushes.Black
            };

            if (message.IsUser)
            {
                textBlock.Text = message.Content;
            }
            else
            {
                // For now, display as plain text (in a full implementation, you'd use a WebView or HTML renderer)
                textBlock.Text = message.Content;
            }

            stackPanel.Children.Add(textBlock);

            // Timestamp
            var timestampText = new TextBlock
            {
                Text = message.Timestamp.ToString("HH:mm"),
                FontSize = 10,
                Foreground = message.IsUser ? Brushes.LightGray : Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0)
            };
            stackPanel.Children.Add(timestampText);

            // Typing indicator
            if (message.IsStreaming)
            {
                var typingIndicator = new TextBlock
                {
                    Text = "▋",
                    FontSize = 14,
                    Foreground = message.IsUser ? Brushes.LightGray : Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                stackPanel.Children.Add(typingIndicator);
            }

            border.Child = stackPanel;
            return border;
        }

        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private void UpdateStatus(string status)
        {
            StatusText.Text = status;
        }

        private void TypingTimer_Tick(object sender, EventArgs e)
        {
            // Handle typing animation if needed
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }
    }

    public class ChatMessage
    {
        public bool IsUser { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsStreaming { get; set; }
    }

    public class AIModel
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ApiUrl { get; set; }
        public bool RequiresApiKey { get; set; }
        public string ApiKeyEnvVar { get; set; }
    }
}
