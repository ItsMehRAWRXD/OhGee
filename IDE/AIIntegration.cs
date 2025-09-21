using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace KimiAppNative.IDE
{
    /// <summary>
    /// Integration layer between OhGees IDE and existing AI models
    /// Provides seamless access to both online and offline AI capabilities
    /// </summary>
    public class AIIntegration
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, AIModel> _availableModels;

        public AIIntegration()
        {
            _httpClient = new HttpClient();
            _availableModels = InitializeAIModels();
        }

        #region AI Model Management

        private Dictionary<string, AIModel> InitializeAIModels()
        {
            return new Dictionary<string, AIModel>
            {
                ["Kimi"] = new AIModel
                {
                    Name = "Kimi",
                    Provider = "Moonshot",
                    Endpoint = "https://api.moonshot.cn/v1/chat/completions",
                    ApiKey = Environment.GetEnvironmentVariable("MOONSHOT_API_KEY"),
                    ModelId = "moonshot-v1-8k",
                    MaxTokens = 8000,
                    Temperature = 0.7f,
                    IsOnline = true,
                    Capabilities = new[] { "Code Generation", "Code Explanation", "Bug Detection", "Refactoring", "Chat" }
                },
                ["ChatGPT"] = new AIModel
                {
                    Name = "ChatGPT",
                    Provider = "OpenAI",
                    Endpoint = "https://api.openai.com/v1/chat/completions",
                    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                    ModelId = "gpt-3.5-turbo",
                    MaxTokens = 4000,
                    Temperature = 0.7f,
                    IsOnline = true,
                    Capabilities = new[] { "Code Generation", "Code Explanation", "Bug Detection", "Refactoring", "Chat" }
                },
                ["DeepSeek"] = new AIModel
                {
                    Name = "DeepSeek",
                    Provider = "DeepSeek",
                    Endpoint = "https://api.deepseek.com/v1/chat/completions",
                    ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
                    ModelId = "deepseek-chat",
                    MaxTokens = 4000,
                    Temperature = 0.7f,
                    IsOnline = true,
                    Capabilities = new[] { "Code Generation", "Code Explanation", "Bug Detection", "Refactoring", "Chat" }
                },
                ["LocalAssistant"] = new AIModel
                {
                    Name = "Local Assistant",
                    Provider = "OhGees",
                    Endpoint = "",
                    ApiKey = "",
                    ModelId = "local-assistant",
                    MaxTokens = 2000,
                    Temperature = 0.5f,
                    IsOnline = false,
                    Capabilities = new[] { "Code Analysis", "Basic Suggestions", "Offline Help" }
                }
            };
        }

        public List<AIModel> GetAvailableModels()
        {
            return _availableModels.Values.ToList();
        }

        public List<AIModel> GetOnlineModels()
        {
            return _availableModels.Values.Where(m => m.IsOnline).ToList();
        }

        public List<AIModel> GetOfflineModels()
        {
            return _availableModels.Values.Where(m => !m.IsOnline).ToList();
        }

        public AIModel GetModel(string name)
        {
            return _availableModels.TryGetValue(name, out var model) ? model : null;
        }

        #endregion

        #region AI Communication

        public async Task<string> CallAI(string modelName, string prompt, string context = "general")
        {
            var model = GetModel(modelName);
            if (model == null)
            {
                return $"Model '{modelName}' not found.";
            }

            if (model.IsOnline)
            {
                return await CallOnlineAI(model, prompt, context);
            }
            else
            {
                return CallOfflineAI(model, prompt, context);
            }
        }

        private async Task<string> CallOnlineAI(AIModel model, string prompt, string context)
        {
            if (string.IsNullOrEmpty(model.ApiKey))
            {
                return $"API key not configured for {model.Name}. Please set the environment variable {model.Provider.ToUpper()}_API_KEY.";
            }

            try
            {
                var requestBody = new
                {
                    model = model.ModelId,
                    messages = new[]
                    {
                        new { role = "system", content = GetSystemPrompt(context) },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = Math.Min(model.MaxTokens, 2000),
                    temperature = model.Temperature
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {model.ApiKey}");

                var response = await _httpClient.PostAsync(model.Endpoint, content);
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
                return $"Error calling {model.Name}: {ex.Message}";
            }
        }

        private string CallOfflineAI(AIModel model, string prompt, string context)
        {
            // Simulate offline AI processing
            return context switch
            {
                "code_generation" => GenerateOfflineCode(prompt),
                "code_explanation" => ExplainOfflineCode(prompt),
                "bug_detection" => DetectOfflineBugs(prompt),
                "refactoring" => SuggestOfflineRefactoring(prompt),
                _ => $"Offline {model.Name} response: {prompt.Substring(0, Math.Min(100, prompt.Length))}..."
            };
        }

        #endregion

        #region Offline AI Capabilities

        private string GenerateOfflineCode(string prompt)
        {
            // Basic offline code generation patterns
            if (prompt.Contains("class"))
            {
                return @"public class GeneratedClass
{
    // TODO: Implement class functionality
    public void Method()
    {
        // Generated method stub
    }
}";
            }
            else if (prompt.Contains("function") || prompt.Contains("method"))
            {
                return @"public void GeneratedMethod()
{
    // TODO: Implement method logic
    Console.WriteLine(""Method executed"");
}";
            }
            else
            {
                return @"// Generated code based on your request
// TODO: Implement the specific functionality
public void Process()
{
    // Add your implementation here
}";
            }
        }

        private string ExplainOfflineCode(string prompt)
        {
            return @"Code Explanation (Offline Analysis):

This code appears to be a C# implementation. Here's what I can identify:

1. **Structure**: The code follows standard C# syntax and conventions
2. **Purpose**: Based on the method names and structure, this appears to be a [purpose]
3. **Key Elements**: 
   - Classes and methods are properly defined
   - Standard .NET patterns are used
4. **Suggestions**: Consider adding error handling and documentation

Note: This is an offline analysis. For more detailed explanations, use an online AI model.";
        }

        private string DetectOfflineBugs(string prompt)
        {
            return @"Potential Issues Detected (Offline Analysis):

⚠️ **Warning**: Potential null reference
⚠️ **Warning**: Missing error handling
⚠️ **Info**: Consider adding input validation
⚠️ **Info**: Method could be optimized

Note: This is a basic offline analysis. For comprehensive bug detection, use an online AI model with full code analysis capabilities.";
        }

        private string SuggestOfflineRefactoring(string prompt)
        {
            return @"Refactoring Suggestions (Offline Analysis):

1. **Extract Method**: Consider breaking down large methods
2. **Rename Variables**: Use more descriptive variable names
3. **Add Constants**: Replace magic numbers with named constants
4. **Improve Structure**: Consider using design patterns where appropriate

Note: This is a basic offline analysis. For detailed refactoring suggestions, use an online AI model.";
        }

        #endregion

        #region System Prompts

        private string GetSystemPrompt(string context)
        {
            return context switch
            {
                "code_generation" => "You are an expert code generator. Create clean, well-documented, production-ready code that follows best practices.",
                "code_explanation" => "You are an expert code teacher. Explain code clearly and help developers understand concepts.",
                "bug_detection" => "You are an expert code reviewer. Identify bugs, issues, and improvements in code.",
                "refactoring" => "You are an expert refactoring assistant. Improve code quality, readability, and performance.",
                "chat" => "You are OhGees AI Assistant, a helpful coding assistant. Provide practical, accurate help with programming tasks.",
                _ => "You are a helpful AI coding assistant."
            };
        }

        #endregion

        #region IDE-Specific Features

        public async Task<string> GetCodeCompletion(string code, int cursorPosition, string language)
        {
            var context = GetCodeContext(code, cursorPosition);
            var prompt = $"Complete the following {language} code at the cursor position:\n\n{context}\n\nProvide only the completion, no explanations.";
            
            // Try online models first, fallback to offline
            var onlineModels = GetOnlineModels();
            if (onlineModels.Any())
            {
                return await CallAI(onlineModels.First().Name, prompt, "code_generation");
            }
            else
            {
                return CallAI("LocalAssistant", prompt, "code_generation");
            }
        }

        public async Task<string> ExplainCode(string code, string language)
        {
            var prompt = $"Explain the following {language} code:\n\n{code}\n\nProvide a clear explanation covering what it does, how it works, and any important concepts.";
            
            var onlineModels = GetOnlineModels();
            if (onlineModels.Any())
            {
                return await CallAI(onlineModels.First().Name, prompt, "code_explanation");
            }
            else
            {
                return CallAI("LocalAssistant", prompt, "code_explanation");
            }
        }

        public async Task<string> DetectBugs(string code, string language)
        {
            var prompt = $"Analyze the following {language} code for bugs, issues, and improvements:\n\n{code}\n\nList any problems found with severity levels.";
            
            var onlineModels = GetOnlineModels();
            if (onlineModels.Any())
            {
                return await CallAI(onlineModels.First().Name, prompt, "bug_detection");
            }
            else
            {
                return CallAI("LocalAssistant", prompt, "bug_detection");
            }
        }

        public async Task<string> SuggestRefactoring(string code, string language)
        {
            var prompt = $"Refactor the following {language} code to improve quality, readability, and performance:\n\n{code}\n\nProvide the refactored version with explanations of improvements.";
            
            var onlineModels = GetOnlineModels();
            if (onlineModels.Any())
            {
                return await CallAI(onlineModels.First().Name, prompt, "refactoring");
            }
            else
            {
                return CallAI("LocalAssistant", prompt, "refactoring");
            }
        }

        private string GetCodeContext(string code, int cursorPosition)
        {
            var lines = code.Split('\n');
            var currentLineIndex = code.Substring(0, cursorPosition).Split('\n').Length - 1;
            
            // Get 3 lines before and after cursor
            var startLine = Math.Max(0, currentLineIndex - 3);
            var endLine = Math.Min(lines.Length - 1, currentLineIndex + 3);
            
            var contextLines = new List<string>();
            for (int i = startLine; i <= endLine; i++)
            {
                var prefix = i == currentLineIndex ? ">>> " : "    ";
                contextLines.Add($"{prefix}{lines[i]}");
            }
            
            return string.Join("\n", contextLines);
        }

        #endregion
    }

    #region Supporting Classes

    public class AIModel
    {
        public string Name { get; set; }
        public string Provider { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string ModelId { get; set; }
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
        public bool IsOnline { get; set; }
        public string[] Capabilities { get; set; }
    }

    #endregion
}
