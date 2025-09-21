using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Threading;

namespace KimiAppNative.IDE
{
    /// <summary>
    /// Abandoned Resource Collector - Specializes in collecting what others don't want
    /// Focuses on abandoned, deprecated, and unused resources that still have value
    /// </summary>
    public class AbandonedResourceCollector
    {
        private readonly AIScrapingEngine _scrapingEngine;
        private readonly ConcurrentDictionary<string, AbandonedResource> _collectedResources;
        private readonly List<CollectionStrategy> _collectionStrategies;
        private readonly Dictionary<string, ResourcePattern> _resourcePatterns;
        private readonly object _collectionLock = new object();

        public event EventHandler<ResourceCollectedEventArgs> ResourceCollected;
        public event EventHandler<CollectionCompletedEventArgs> CollectionCompleted;

        public AbandonedResourceCollector()
        {
            _scrapingEngine = new AIScrapingEngine();
            _collectedResources = new ConcurrentDictionary<string, AbandonedResource>();
            _collectionStrategies = new List<CollectionStrategy>();
            _resourcePatterns = new Dictionary<string, ResourcePattern>();
            
            InitializeCollectionStrategies();
            InitializeResourcePatterns();
        }

        #region Initialization

        private void InitializeCollectionStrategies()
        {
            _collectionStrategies.AddRange(new[]
            {
                new CollectionStrategy
                {
                    Name = "Abandoned Code Harvesting",
                    Description = "Collect code that was abandoned but still valuable",
                    TargetTypes = new[] { ResourceType.Code, ResourceType.Snippet },
                    Priority = CollectionPriority.High,
                    Filters = new[] { "abandoned", "unused", "deprecated", "legacy" },
                    ValueAssessment = "High - Often contains unique solutions"
                },
                new CollectionStrategy
                {
                    Name = "Orphaned Documentation Mining",
                    Description = "Mine documentation that was left behind",
                    TargetTypes = new[] { ResourceType.Documentation, ResourceType.Tutorial },
                    Priority = CollectionPriority.Medium,
                    Filters = new[] { "orphaned", "outdated", "unmaintained", "archived" },
                    ValueAssessment = "Medium - Contains historical knowledge"
                },
                new CollectionStrategy
                {
                    Name = "Discarded Knowledge Recovery",
                    Description = "Recover knowledge that was discarded",
                    TargetTypes = new[] { ResourceType.QnA, ResourceType.Forum },
                    Priority = CollectionPriority.High,
                    Filters = new[] { "discarded", "ignored", "overlooked", "forgotten" },
                    ValueAssessment = "High - Often contains expert knowledge"
                },
                new CollectionStrategy
                {
                    Name = "Unwanted Innovation Collection",
                    Description = "Collect innovations that were rejected",
                    TargetTypes = new[] { ResourceType.Example, ResourceType.Snippet },
                    Priority = CollectionPriority.Critical,
                    Filters = new[] { "rejected", "unwanted", "innovative", "ahead-of-time" },
                    ValueAssessment = "Critical - Contains future-ready solutions"
                }
            });
        }

        private void InitializeResourcePatterns()
        {
            _resourcePatterns["AbandonedClass"] = new ResourcePattern
            {
                Name = "Abandoned Class Pattern",
                Pattern = @"class\s+\w*Abandoned\w*\s*\{[^}]*\}",
                Description = "Classes marked as abandoned but containing useful code",
                ValueScore = 8,
                ReusabilityScore = 7
            };

            _resourcePatterns["DeprecatedMethod"] = new ResourcePattern
            {
                Name = "Deprecated Method Pattern",
                Pattern = @"\[Obsolete.*?\]\s*public\s+\w+\s+\w+\s*\([^)]*\)",
                Description = "Methods marked as obsolete but still functional",
                ValueScore = 6,
                ReusabilityScore = 8
            };

            _resourcePatterns["UnusedUtility"] = new ResourcePattern
            {
                Name = "Unused Utility Pattern",
                Pattern = @"public\s+static\s+class\s+\w*Util\w*\s*\{[^}]*\}",
                Description = "Utility classes that were never used",
                ValueScore = 9,
                ReusabilityScore = 9
            };

            _resourcePatterns["LegacyAlgorithm"] = new ResourcePattern
            {
                Name = "Legacy Algorithm Pattern",
                Pattern = @"//\s*Legacy.*?algorithm.*?\n.*?public\s+\w+.*?\{[^}]*\}",
                Description = "Algorithms marked as legacy but still effective",
                ValueScore = 7,
                ReusabilityScore = 6
            };
        }

        #endregion

        #region Collection Operations

        public async Task<string> StartCollectionSession(string sessionName, CollectionScope scope = CollectionScope.All)
        {
            var sessionId = Guid.NewGuid().ToString();
            
            Console.WriteLine($"[Abandoned Collector] Starting collection session: {sessionName} (ID: {sessionId})");
            
            var strategies = _collectionStrategies.Where(s => IsStrategyInScope(s, scope)).ToList();
            
            foreach (var strategy in strategies)
            {
                await ExecuteCollectionStrategy(strategy, sessionId);
            }
            
            CollectionCompleted?.Invoke(this, new CollectionCompletedEventArgs
            {
                SessionId = sessionId,
                ResourcesCollected = _collectedResources.Count,
                Success = true
            });
            
            return sessionId;
        }

        public async Task<AbandonedResource> CollectAbandonedCode(string source)
        {
            try
            {
                Console.WriteLine($"[Abandoned Collector] Collecting abandoned code from: {source}");
                
                var resource = new AbandonedResource
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Type = ResourceType.Code,
                    CollectedAt = DateTime.Now,
                    Status = CollectionStatus.Processing,
                    AbandonmentReason = "Unknown - requires analysis"
                };
                
                // Analyze the abandoned code
                var analysis = await AnalyzeAbandonedCode(source);
                
                resource.Content = analysis.Content;
                resource.Metadata = analysis.Metadata;
                resource.ValueScore = analysis.ValueScore;
                resource.ReusabilityScore = analysis.ReusabilityScore;
                resource.Tags = analysis.Tags;
                resource.Status = CollectionStatus.Collected;
                
                _collectedResources[resource.Id] = resource;
                
                ResourceCollected?.Invoke(this, new ResourceCollectedEventArgs
                {
                    Resource = resource,
                    Success = true
                });
                
                return resource;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Abandoned Collector] Error collecting code: {ex.Message}");
                return null;
            }
        }

        public async Task<AbandonedResource> CollectDiscardedKnowledge(string source)
        {
            try
            {
                Console.WriteLine($"[Abandoned Collector] Collecting discarded knowledge from: {source}");
                
                var resource = new AbandonedResource
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Type = ResourceType.Documentation,
                    CollectedAt = DateTime.Now,
                    Status = CollectionStatus.Processing,
                    AbandonmentReason = "Knowledge was discarded or overlooked"
                };
                
                var analysis = await AnalyzeDiscardedKnowledge(source);
                
                resource.Content = analysis.Content;
                resource.Metadata = analysis.Metadata;
                resource.ValueScore = analysis.ValueScore;
                resource.ReusabilityScore = analysis.ReusabilityScore;
                resource.Tags = analysis.Tags;
                resource.Status = CollectionStatus.Collected;
                
                _collectedResources[resource.Id] = resource;
                
                ResourceCollected?.Invoke(this, new ResourceCollectedEventArgs
                {
                    Resource = resource,
                    Success = true
                });
                
                return resource;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Abandoned Collector] Error collecting knowledge: {ex.Message}");
                return null;
            }
        }

        public async Task<AbandonedResource> CollectUnwantedInnovation(string source)
        {
            try
            {
                Console.WriteLine($"[Abandoned Collector] Collecting unwanted innovation from: {source}");
                
                var resource = new AbandonedResource
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Type = ResourceType.Example,
                    CollectedAt = DateTime.Now,
                    Status = CollectionStatus.Processing,
                    AbandonmentReason = "Innovation was ahead of its time or rejected"
                };
                
                var analysis = await AnalyzeUnwantedInnovation(source);
                
                resource.Content = analysis.Content;
                resource.Metadata = analysis.Metadata;
                resource.ValueScore = analysis.ValueScore;
                resource.ReusabilityScore = analysis.ReusabilityScore;
                resource.Tags = analysis.Tags;
                resource.Status = CollectionStatus.Collected;
                
                _collectedResources[resource.Id] = resource;
                
                ResourceCollected?.Invoke(this, new ResourceCollectedEventArgs
                {
                    Resource = resource,
                    Success = true
                });
                
                return resource;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Abandoned Collector] Error collecting innovation: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Analysis Methods

        private async Task<ResourceAnalysis> AnalyzeAbandonedCode(string source)
        {
            await Task.Delay(1500); // Simulate analysis time
            
            var content = new StringBuilder();
            var metadata = new Dictionary<string, object>();
            var tags = new List<string>();
            
            // Simulate finding abandoned code patterns
            content.AppendLine("// Abandoned Code Analysis Results");
            content.AppendLine("// This code was abandoned but contains valuable patterns");
            content.AppendLine();
            content.AppendLine("public class AbandonedDataProcessor");
            content.AppendLine("{");
            content.AppendLine("    // This method was abandoned because it was 'too complex'");
            content.AppendLine("    // But it actually contains an elegant solution");
            content.AppendLine("    public static T ProcessAbandonedData<T>(IEnumerable<T> data, Func<T, bool> filter)");
            content.AppendLine("    {");
            content.AppendLine("        return data.Where(filter).FirstOrDefault();");
            content.AppendLine("    }");
            content.AppendLine("    ");
            content.AppendLine("    // This algorithm was discarded for being 'inefficient'");
            content.AppendLine("    // But it's actually more memory-efficient than the 'improved' version");
            content.AppendLine("    public static void AbandonedSort<T>(T[] array) where T : IComparable<T>");
            content.AppendLine("    {");
            content.AppendLine("        // Custom sorting algorithm that was rejected");
            content.AppendLine("        for (int i = 0; i < array.Length - 1; i++)");
            content.AppendLine("        {");
            content.AppendLine("            for (int j = 0; j < array.Length - i - 1; j++)");
            content.AppendLine("            {");
            content.AppendLine("                if (array[j].CompareTo(array[j + 1]) > 0)");
            content.AppendLine("                {");
            content.AppendLine("                    var temp = array[j];");
            content.AppendLine("                    array[j] = array[j + 1];");
            content.AppendLine("                    array[j + 1] = temp;");
            content.AppendLine("                }");
            content.AppendLine("            }");
            content.AppendLine("        }");
            content.AppendLine("    }");
            content.AppendLine("}");
            
            metadata["Language"] = "C#";
            metadata["AbandonmentReason"] = "Considered too complex or inefficient";
            metadata["OriginalValue"] = "High - Contains unique algorithms";
            metadata["Reusability"] = "High - Can be adapted for modern use";
            metadata["LinesOfCode"] = 25;
            metadata["Complexity"] = "Medium";
            
            tags.AddRange(new[] { "abandoned", "csharp", "algorithm", "efficient", "reusable" });
            
            return new ResourceAnalysis
            {
                Content = content.ToString(),
                Metadata = metadata,
                ValueScore = 8,
                ReusabilityScore = 9,
                Tags = tags.ToArray()
            };
        }

        private async Task<ResourceAnalysis> AnalyzeDiscardedKnowledge(string source)
        {
            await Task.Delay(1200);
            
            var content = new StringBuilder();
            var metadata = new Dictionary<string, object>();
            var tags = new List<string>();
            
            content.AppendLine("# Discarded Knowledge Recovery");
            content.AppendLine();
            content.AppendLine("## Why Knowledge Gets Discarded");
            content.AppendLine();
            content.AppendLine("1. **Too Advanced**: Knowledge that's ahead of its time");
            content.AppendLine("2. **Too Niche**: Specialized knowledge that seems irrelevant");
            content.AppendLine("3. **Too Complex**: Solutions that seem overly complicated");
            content.AppendLine("4. **Too Simple**: Solutions that seem too basic");
            content.AppendLine("5. **Wrong Context**: Knowledge applied in wrong situation");
            content.AppendLine();
            content.AppendLine("## Recovered Techniques");
            content.AppendLine();
            content.AppendLine("### Technique 1: Abandoned Optimization Patterns");
            content.AppendLine();
            content.AppendLine("```csharp");
            content.AppendLine("// This pattern was abandoned for being 'too clever'");
            content.AppendLine("// But it's actually more efficient than current approaches");
            content.AppendLine("public static class AbandonedOptimizations");
            content.AppendLine("{");
            content.AppendLine("    public static void ProcessInChunks<T>(IEnumerable<T> items, int chunkSize, Action<IEnumerable<T>> processor)");
            content.AppendLine("    {");
            content.AppendLine("        var chunks = items.Chunk(chunkSize);");
            content.AppendLine("        foreach (var chunk in chunks)");
            content.AppendLine("        {");
            content.AppendLine("            processor(chunk);");
            content.AppendLine("        }");
            content.AppendLine("    }");
            content.AppendLine("}");
            content.AppendLine("```");
            content.AppendLine();
            content.AppendLine("### Technique 2: Discarded Error Handling");
            content.AppendLine();
            content.AppendLine("```csharp");
            content.AppendLine("// This error handling was considered 'too verbose'");
            content.AppendLine("// But it provides better debugging information");
            content.AppendLine("public static class DiscardedErrorHandling");
            content.AppendLine("{");
            content.AppendLine("    public static T ExecuteWithDetailedErrorHandling<T>(Func<T> operation, string context)");
            content.AppendLine("    {");
            content.AppendLine("        try");
            content.AppendLine("        {");
            content.AppendLine("            return operation();");
            content.AppendLine("        }");
            content.AppendLine("        catch (Exception ex)");
            content.AppendLine("        {");
            content.AppendLine("            var errorInfo = new");
            content.AppendLine("            {");
            content.AppendLine("                Context = context,");
            content.AppendLine("                Timestamp = DateTime.Now,");
            content.AppendLine("                Exception = ex,");
            content.AppendLine("                StackTrace = ex.StackTrace");
            content.AppendLine("            };");
            content.AppendLine("            // Log detailed error information");
            content.AppendLine("            throw new DetailedException(errorInfo, ex);");
            content.AppendLine("        }");
            content.AppendLine("    }");
            content.AppendLine("}");
            content.AppendLine("```");
            
            metadata["KnowledgeType"] = "Programming Techniques";
            metadata["DiscardReason"] = "Considered too advanced or verbose";
            metadata["RecoveryValue"] = "High - Contains expert-level knowledge";
            metadata["ApplicationAreas"] = new[] { "Performance", "Error Handling", "Optimization" };
            
            tags.AddRange(new[] { "discarded", "knowledge", "advanced", "optimization", "error-handling" });
            
            return new ResourceAnalysis
            {
                Content = content.ToString(),
                Metadata = metadata,
                ValueScore = 9,
                ReusabilityScore = 8,
                Tags = tags.ToArray()
            };
        }

        private async Task<ResourceAnalysis> AnalyzeUnwantedInnovation(string source)
        {
            await Task.Delay(1800);
            
            var content = new StringBuilder();
            var metadata = new Dictionary<string, object>();
            var tags = new List<string>();
            
            content.AppendLine("# Unwanted Innovation Collection");
            content.AppendLine();
            content.AppendLine("## Innovations That Were Rejected");
            content.AppendLine();
            content.AppendLine("Many groundbreaking innovations are rejected because:");
            content.AppendLine("- They're ahead of their time");
            content.AppendLine("- They challenge existing paradigms");
            content.AppendLine("- They seem too risky");
            content.AppendLine("- They don't fit current trends");
            content.AppendLine();
            content.AppendLine("## Recovered Innovations");
            content.AppendLine();
            content.AppendLine("### Innovation 1: Abandoned Architecture Pattern");
            content.AppendLine();
            content.AppendLine("```csharp");
            content.AppendLine("// This architecture was rejected for being 'too complex'");
            content.AppendLine("// But it's actually more maintainable than current approaches");
            content.AppendLine("public class AbandonedArchitecture");
            content.AppendLine("{");
            content.AppendLine("    // Micro-service pattern that was ahead of its time");
            content.AppendLine("    public interface IServiceNode");
            content.AppendLine("    {");
            content.AppendLine("        Task<object> ProcessAsync(object input);");
            content.AppendLine("        bool CanHandle(object input);");
            content.AppendLine("    }");
            content.AppendLine("    ");
            content.AppendLine("    public class ServiceOrchestrator");
            content.AppendLine("    {");
            content.AppendLine("        private readonly List<IServiceNode> _nodes;");
            content.AppendLine("        ");
            content.AppendLine("        public async Task<object> ProcessAsync(object input)");
            content.AppendLine("        {");
            content.AppendLine("            var node = _nodes.FirstOrDefault(n => n.CanHandle(input));");
            content.AppendLine("            return node != null ? await node.ProcessAsync(input) : null;");
            content.AppendLine("        }");
            content.AppendLine("    }");
            content.AppendLine("}");
            content.AppendLine("```");
            content.AppendLine();
            content.AppendLine("### Innovation 2: Rejected Caching Strategy");
            content.AppendLine();
            content.AppendLine("```csharp");
            content.AppendLine("// This caching strategy was rejected for being 'too memory intensive'");
            content.AppendLine("// But it's actually more efficient than current solutions");
            content.AppendLine("public class RejectedCachingStrategy");
            content.AppendLine("{");
            content.AppendLine("    private readonly Dictionary<string, CacheEntry> _cache;");
            content.AppendLine("    private readonly TimeSpan _defaultTtl;");
            content.AppendLine("    ");
            content.AppendLine("    public T GetOrSet<T>(string key, Func<T> factory, TimeSpan? ttl = null)");
            content.AppendLine("    {");
            content.AppendLine("        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)");
            content.AppendLine("        {");
            content.AppendLine("            return (T)entry.Value;");
            content.AppendLine("        }");
            content.AppendLine("        ");
            content.AppendLine("        var value = factory();");
            content.AppendLine("        _cache[key] = new CacheEntry(value, ttl ?? _defaultTtl);");
            content.AppendLine("        return value;");
            content.AppendLine("    }");
            content.AppendLine("}");
            content.AppendLine("```");
            
            metadata["InnovationType"] = "Architecture and Caching";
            metadata["RejectionReason"] = "Ahead of its time or too complex";
            metadata["CurrentRelevance"] = "High - Now widely adopted";
            metadata["InnovationLevel"] = "Groundbreaking";
            
            tags.AddRange(new[] { "innovation", "rejected", "architecture", "caching", "ahead-of-time" });
            
            return new ResourceAnalysis
            {
                Content = content.ToString(),
                Metadata = metadata,
                ValueScore = 10,
                ReusabilityScore = 9,
                Tags = tags.ToArray()
            };
        }

        #endregion

        #region Strategy Execution

        private async Task ExecuteCollectionStrategy(CollectionStrategy strategy, string sessionId)
        {
            Console.WriteLine($"[Abandoned Collector] Executing strategy: {strategy.Name}");
            
            foreach (var targetType in strategy.TargetTypes)
            {
                await CollectResourcesByType(targetType, strategy, sessionId);
            }
        }

        private async Task CollectResourcesByType(ResourceType type, CollectionStrategy strategy, string sessionId)
        {
            // Simulate collecting resources of specific type
            await Task.Delay(1000);
            
            var resource = new AbandonedResource
            {
                Id = Guid.NewGuid().ToString(),
                Source = $"Strategy: {strategy.Name}",
                Type = type,
                CollectedAt = DateTime.Now,
                Status = CollectionStatus.Collected,
                AbandonmentReason = strategy.Description,
                ValueScore = CalculateValueScore(strategy),
                ReusabilityScore = CalculateReusabilityScore(strategy),
                Tags = strategy.Filters
            };
            
            _collectedResources[resource.Id] = resource;
            
            Console.WriteLine($"[Abandoned Collector] Collected {type} resource using {strategy.Name}");
        }

        private int CalculateValueScore(CollectionStrategy strategy)
        {
            return strategy.Priority switch
            {
                CollectionPriority.Critical => 10,
                CollectionPriority.High => 8,
                CollectionPriority.Medium => 6,
                CollectionPriority.Low => 4,
                _ => 5
            };
        }

        private int CalculateReusabilityScore(CollectionStrategy strategy)
        {
            return strategy.ValueAssessment.Contains("High") ? 9 :
                   strategy.ValueAssessment.Contains("Medium") ? 6 :
                   strategy.ValueAssessment.Contains("Low") ? 3 : 5;
        }

        #endregion

        #region Utility Methods

        private bool IsStrategyInScope(CollectionStrategy strategy, CollectionScope scope)
        {
            return scope switch
            {
                CollectionScope.All => true,
                CollectionScope.HighValue => strategy.Priority == CollectionPriority.High || strategy.Priority == CollectionPriority.Critical,
                CollectionScope.CodeOnly => strategy.TargetTypes.Contains(ResourceType.Code),
                CollectionScope.KnowledgeOnly => strategy.TargetTypes.Contains(ResourceType.Documentation) || strategy.TargetTypes.Contains(ResourceType.QnA),
                _ => true
            };
        }

        #endregion

        #region Resource Management

        public List<AbandonedResource> GetCollectedResources(ResourceType? type = null)
        {
            return _collectedResources.Values
                .Where(r => type == null || r.Type == type)
                .ToList();
        }

        public List<AbandonedResource> GetHighValueResources()
        {
            return _collectedResources.Values
                .Where(r => r.ValueScore >= 8)
                .OrderByDescending(r => r.ValueScore)
                .ToList();
        }

        public List<AbandonedResource> GetReusableResources()
        {
            return _collectedResources.Values
                .Where(r => r.ReusabilityScore >= 7)
                .OrderByDescending(r => r.ReusabilityScore)
                .ToList();
        }

        public Dictionary<string, object> GetCollectionStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalCollected"] = _collectedResources.Count,
                ["ResourcesByType"] = _collectedResources.Values
                    .GroupBy(r => r.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ["AverageValueScore"] = _collectedResources.Values.Average(r => r.ValueScore),
                ["AverageReusabilityScore"] = _collectedResources.Values.Average(r => r.ReusabilityScore),
                ["HighValueResources"] = _collectedResources.Values.Count(r => r.ValueScore >= 8),
                ["HighlyReusableResources"] = _collectedResources.Values.Count(r => r.ReusabilityScore >= 7)
            };
        }

        #endregion
    }

    #region Supporting Classes and Enums

    public class CollectionStrategy
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ResourceType[] TargetTypes { get; set; }
        public CollectionPriority Priority { get; set; }
        public string[] Filters { get; set; }
        public string ValueAssessment { get; set; }
    }

    public class ResourcePattern
    {
        public string Name { get; set; }
        public string Pattern { get; set; }
        public string Description { get; set; }
        public int ValueScore { get; set; }
        public int ReusabilityScore { get; set; }
    }

    public class AbandonedResource
    {
        public string Id { get; set; }
        public string Source { get; set; }
        public ResourceType Type { get; set; }
        public string Content { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public string[] Tags { get; set; }
        public CollectionStatus Status { get; set; }
        public DateTime CollectedAt { get; set; }
        public string AbandonmentReason { get; set; }
        public int ValueScore { get; set; }
        public int ReusabilityScore { get; set; }
    }

    public class ResourceAnalysis
    {
        public string Content { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public int ValueScore { get; set; }
        public int ReusabilityScore { get; set; }
        public string[] Tags { get; set; }
    }

    public enum CollectionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum CollectionScope
    {
        All,
        HighValue,
        CodeOnly,
        KnowledgeOnly
    }

    public enum CollectionStatus
    {
        Processing,
        Collected,
        Failed,
        Archived
    }

    public class ResourceCollectedEventArgs : EventArgs
    {
        public AbandonedResource Resource { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class CollectionCompletedEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public int ResourcesCollected { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    #endregion
}
