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
            ModelComboBox.SelectedItem = _availableModels.FirstOrDefault(m => m.Name == "openai");
            
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
                },
                new AIModel
                {
                    Name = "mock",
                    DisplayName = "🎭 Mock Response (Demo)",
                    ApiUrl = "",
                    RequiresApiKey = false,
                    ApiKeyEnvVar = ""
                }
            };
        }

        private void LoadWelcomeMessage()
        {
            var welcomeMessage = new ChatMessage
            {
                IsUser = false,
                Content = "👋 Hello! I'm your AI assistant. I can help you with various tasks including:\n\n" +
                         "• **Code assistance** - Writing, debugging, and explaining code\n" +
                         "• **Creative writing** - Stories, articles, and content creation\n" +
                         "• **Problem solving** - Math, logic, and analytical questions\n" +
                         "• **Learning support** - Explanations and educational content\n\n" +
                         "Choose a model from the dropdown above and start chatting!",
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
            if (_currentModel.Name == "mock")
            {
                await SimulateTypingResponse(aiMessage, GetMockResponse(userMessage));
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

        private async Task SimulateTypingResponse(ChatMessage aiMessage, string response)
        {
            _isTyping = true;
            var words = response.Split(' ');
            var currentContent = new StringBuilder();

            foreach (var word in words)
            {
                if (!_isTyping) break;
                
                currentContent.Append(word + " ");
                aiMessage.Content = currentContent.ToString();
                UpdateMessageUI(aiMessage);
                
                await Task.Delay(100); // Typing delay
            }

            aiMessage.IsStreaming = false;
            UpdateMessageUI(aiMessage);
        }

        private string GetMockResponse(string userMessage)
        {
            var responses = new[]
            {
                "That's an interesting question! Let me think about this...\n\nBased on what you've asked, I believe the key points to consider are:\n\n1. **Understanding the context** - It's important to fully grasp the situation\n2. **Analyzing the options** - Consider all possible approaches\n3. **Making a decision** - Choose the best path forward\n\nWould you like me to elaborate on any of these points?",
                
                "Great question! Here's my analysis:\n\n```python\n# Example code snippet\ndef analyze_problem(input_data):\n    result = process_data(input_data)\n    return result\n```\n\nThis approach should help you achieve your goal. Let me know if you need more details!",
                
                "I understand what you're looking for. Here's a comprehensive response:\n\n## Key Insights\n\n- **First point**: This is crucial for success\n- **Second point**: Don't overlook this aspect\n- **Third point**: This ties everything together\n\n### Next Steps\n1. Review the information above\n2. Consider your specific situation\n3. Take action based on your needs\n\nIs there anything specific you'd like me to clarify?",
                
                "Excellent question! Let me break this down for you:\n\n> **Important Note**: This is a complex topic that requires careful consideration.\n\n**Here's what I recommend:**\n\n- Start with the basics\n- Build up your understanding gradually\n- Practice with real examples\n- Don't hesitate to ask follow-up questions\n\nI'm here to help you succeed! What would you like to explore next?"
            };

            return responses[new Random().Next(responses.Length)];
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
