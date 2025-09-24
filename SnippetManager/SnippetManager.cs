using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KimiAppNative.SnippetManager
{
    /// <summary>
    /// Manages code snippets and templates
    /// </summary>
    public class SnippetManager
    {
        private readonly string _snippetsPath;
        private readonly string _templatesPath;
        private readonly ObservableCollection<CodeSnippet> _snippets;
        private readonly ObservableCollection<CodeTemplate> _templates;
        private readonly Dictionary<string, List<CodeSnippet>> _categorizedSnippets;
        private readonly JsonSerializerOptions _jsonOptions;

        public ObservableCollection<CodeSnippet> Snippets => _snippets;
        public ObservableCollection<CodeTemplate> Templates => _templates;
        public IReadOnlyDictionary<string, List<CodeSnippet>> CategorizedSnippets => _categorizedSnippets;

        public event EventHandler<SnippetEventArgs>? SnippetAdded;
        public event EventHandler<SnippetEventArgs>? SnippetUpdated;
        public event EventHandler<SnippetEventArgs>? SnippetDeleted;
        public event EventHandler<TemplateEventArgs>? TemplateAdded;
        public event EventHandler? CollectionChanged;

        public SnippetManager()
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OhGee"
            );

            _snippetsPath = Path.Combine(basePath, "Snippets");
            _templatesPath = Path.Combine(basePath, "Templates");
            
            Directory.CreateDirectory(_snippetsPath);
            Directory.CreateDirectory(_templatesPath);
            
            _snippets = new ObservableCollection<CodeSnippet>();
            _templates = new ObservableCollection<CodeTemplate>();
            _categorizedSnippets = new Dictionary<string, List<CodeSnippet>>();
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            
            LoadSnippetsAndTemplates();
            InitializeDefaultSnippets();
        }

        /// <summary>
        /// Adds a new code snippet
        /// </summary>
        public async Task<CodeSnippet> AddSnippetAsync(CodeSnippet snippet)
        {
            if (string.IsNullOrEmpty(snippet.Id))
            {
                snippet.Id = Guid.NewGuid().ToString();
            }
            
            snippet.CreatedAt = DateTime.Now;
            snippet.LastModified = DateTime.Now;
            
            _snippets.Add(snippet);
            CategorizeSnippet(snippet);
            
            await SaveSnippetAsync(snippet);
            SnippetAdded?.Invoke(this, new SnippetEventArgs(snippet));
            
            return snippet;
        }

        /// <summary>
        /// Updates an existing snippet
        /// </summary>
        public async Task UpdateSnippetAsync(CodeSnippet snippet)
        {
            var existing = _snippets.FirstOrDefault(s => s.Id == snippet.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Snippet {snippet.Id} not found");
            }
            
            snippet.LastModified = DateTime.Now;
            
            var index = _snippets.IndexOf(existing);
            _snippets[index] = snippet;
            
            RecategorizeSnippets();
            
            await SaveSnippetAsync(snippet);
            SnippetUpdated?.Invoke(this, new SnippetEventArgs(snippet));
        }

        /// <summary>
        /// Deletes a snippet
        /// </summary>
        public async Task DeleteSnippetAsync(string snippetId)
        {
            var snippet = _snippets.FirstOrDefault(s => s.Id == snippetId);
            if (snippet == null)
            {
                return;
            }
            
            _snippets.Remove(snippet);
            RecategorizeSnippets();
            
            var filePath = Path.Combine(_snippetsPath, $"{snippetId}.json");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
            
            SnippetDeleted?.Invoke(this, new SnippetEventArgs(snippet));
        }

        /// <summary>
        /// Searches snippets
        /// </summary>
        public List<CodeSnippet> SearchSnippets(string query, SearchOptions? options = null)
        {
            options ??= new SearchOptions();
            
            IEnumerable<CodeSnippet> results = _snippets;
            
            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();
                results = results.Where(s =>
                    s.Title.ToLower().Contains(query) ||
                    s.Description.ToLower().Contains(query) ||
                    s.Code.ToLower().Contains(query) ||
                    s.Tags.Any(t => t.ToLower().Contains(query))
                );
            }
            
            if (!string.IsNullOrEmpty(options.Language))
            {
                results = results.Where(s => s.Language.Equals(options.Language, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrEmpty(options.Category))
            {
                results = results.Where(s => s.Category.Equals(options.Category, StringComparison.OrdinalIgnoreCase));
            }
            
            if (options.Tags?.Any() == true)
            {
                results = results.Where(s => options.Tags.Any(tag => s.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
            }
            
            if (options.IsFavorite.HasValue)
            {
                results = results.Where(s => s.IsFavorite == options.IsFavorite.Value);
            }
            
            // Apply sorting
            results = options.SortBy switch
            {
                SortBy.Title => options.SortDescending ? results.OrderByDescending(s => s.Title) : results.OrderBy(s => s.Title),
                SortBy.Created => options.SortDescending ? results.OrderByDescending(s => s.CreatedAt) : results.OrderBy(s => s.CreatedAt),
                SortBy.Modified => options.SortDescending ? results.OrderByDescending(s => s.LastModified) : results.OrderBy(s => s.LastModified),
                SortBy.Usage => options.SortDescending ? results.OrderByDescending(s => s.UsageCount) : results.OrderBy(s => s.UsageCount),
                _ => results.OrderByDescending(s => s.LastModified)
            };
            
            return results.ToList();
        }

        /// <summary>
        /// Gets snippet by ID
        /// </summary>
        public CodeSnippet? GetSnippet(string snippetId)
        {
            return _snippets.FirstOrDefault(s => s.Id == snippetId);
        }

        /// <summary>
        /// Increments usage count for a snippet
        /// </summary>
        public async Task IncrementUsageAsync(string snippetId)
        {
            var snippet = GetSnippet(snippetId);
            if (snippet != null)
            {
                snippet.UsageCount++;
                snippet.LastUsed = DateTime.Now;
                await SaveSnippetAsync(snippet);
            }
        }

        /// <summary>
        /// Toggles favorite status
        /// </summary>
        public async Task ToggleFavoriteAsync(string snippetId)
        {
            var snippet = GetSnippet(snippetId);
            if (snippet != null)
            {
                snippet.IsFavorite = !snippet.IsFavorite;
                await SaveSnippetAsync(snippet);
                SnippetUpdated?.Invoke(this, new SnippetEventArgs(snippet));
            }
        }

        /// <summary>
        /// Creates a template from a snippet
        /// </summary>
        public async Task<CodeTemplate> CreateTemplateFromSnippetAsync(string snippetId, string templateName)
        {
            var snippet = GetSnippet(snippetId);
            if (snippet == null)
            {
                throw new InvalidOperationException($"Snippet {snippetId} not found");
            }
            
            var template = new CodeTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = templateName,
                Description = snippet.Description,
                Language = snippet.Language,
                Category = snippet.Category,
                Content = snippet.Code,
                Variables = ExtractTemplateVariables(snippet.Code),
                CreatedAt = DateTime.Now
            };
            
            _templates.Add(template);
            await SaveTemplateAsync(template);
            TemplateAdded?.Invoke(this, new TemplateEventArgs(template));
            
            return template;
        }

        /// <summary>
        /// Applies a template with variables
        /// </summary>
        public string ApplyTemplate(string templateId, Dictionary<string, string> variables)
        {
            var template = _templates.FirstOrDefault(t => t.Id == templateId);
            if (template == null)
            {
                throw new InvalidOperationException($"Template {templateId} not found");
            }
            
            var result = template.Content;
            
            foreach (var variable in variables)
            {
                result = result.Replace($"{{{{{variable.Key}}}}}", variable.Value);
            }
            
            return result;
        }

        /// <summary>
        /// Exports snippets to file
        /// </summary>
        public async Task ExportSnippetsAsync(string filePath, ExportFormat format = ExportFormat.Json)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    var json = JsonSerializer.Serialize(_snippets.ToList(), _jsonOptions);
                    await File.WriteAllTextAsync(filePath, json);
                    break;
                    
                case ExportFormat.Markdown:
                    var markdown = GenerateMarkdownExport();
                    await File.WriteAllTextAsync(filePath, markdown);
                    break;
                    
                case ExportFormat.VSCode:
                    var vscodeSnippets = ConvertToVSCodeFormat();
                    await File.WriteAllTextAsync(filePath, vscodeSnippets);
                    break;
            }
        }

        /// <summary>
        /// Imports snippets from file
        /// </summary>
        public async Task<int> ImportSnippetsAsync(string filePath, bool overwriteExisting = false)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File {filePath} not found");
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            var snippets = JsonSerializer.Deserialize<List<CodeSnippet>>(json, _jsonOptions);
            
            if (snippets == null)
            {
                return 0;
            }
            
            int imported = 0;
            
            foreach (var snippet in snippets)
            {
                var existing = GetSnippet(snippet.Id);
                
                if (existing != null)
                {
                    if (overwriteExisting)
                    {
                        await UpdateSnippetAsync(snippet);
                        imported++;
                    }
                }
                else
                {
                    await AddSnippetAsync(snippet);
                    imported++;
                }
            }
            
            return imported;
        }

        /// <summary>
        /// Gets snippet statistics
        /// </summary>
        public SnippetStatistics GetStatistics()
        {
            return new SnippetStatistics
            {
                TotalSnippets = _snippets.Count,
                TotalTemplates = _templates.Count,
                FavoriteCount = _snippets.Count(s => s.IsFavorite),
                LanguageDistribution = _snippets.GroupBy(s => s.Language)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CategoryDistribution = _snippets.GroupBy(s => s.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MostUsedSnippets = _snippets.OrderByDescending(s => s.UsageCount)
                    .Take(10)
                    .ToList(),
                RecentlyUsedSnippets = _snippets.Where(s => s.LastUsed.HasValue)
                    .OrderByDescending(s => s.LastUsed)
                    .Take(10)
                    .ToList()
            };
        }

        /// <summary>
        /// Saves a snippet to disk
        /// </summary>
        private async Task SaveSnippetAsync(CodeSnippet snippet)
        {
            var filePath = Path.Combine(_snippetsPath, $"{snippet.Id}.json");
            var json = JsonSerializer.Serialize(snippet, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves a template to disk
        /// </summary>
        private async Task SaveTemplateAsync(CodeTemplate template)
        {
            var filePath = Path.Combine(_templatesPath, $"{template.Id}.json");
            var json = JsonSerializer.Serialize(template, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Loads snippets and templates from disk
        /// </summary>
        private void LoadSnippetsAndTemplates()
        {
            // Load snippets
            if (Directory.Exists(_snippetsPath))
            {
                var snippetFiles = Directory.GetFiles(_snippetsPath, "*.json");
                foreach (var file in snippetFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var snippet = JsonSerializer.Deserialize<CodeSnippet>(json, _jsonOptions);
                        if (snippet != null)
                        {
                            _snippets.Add(snippet);
                        }
                    }
                    catch
                    {
                        // Skip corrupted files
                    }
                }
            }
            
            // Load templates
            if (Directory.Exists(_templatesPath))
            {
                var templateFiles = Directory.GetFiles(_templatesPath, "*.json");
                foreach (var file in templateFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<CodeTemplate>(json, _jsonOptions);
                        if (template != null)
                        {
                            _templates.Add(template);
                        }
                    }
                    catch
                    {
                        // Skip corrupted files
                    }
                }
            }
            
            RecategorizeSnippets();
        }

        /// <summary>
        /// Initializes default snippets
        /// </summary>
        private void InitializeDefaultSnippets()
        {
            if (_snippets.Any())
                return;
            
            // Add some default snippets
            var defaultSnippets = new List<CodeSnippet>
            {
                new CodeSnippet
                {
                    Title = "Async Method Template",
                    Description = "Basic async method structure",
                    Language = "C#",
                    Category = "Methods",
                    Code = @"public async Task<T> MethodNameAsync(parameters)
{
    try
    {
        // Implementation
        await Task.Delay(100);
        return default(T);
    }
    catch (Exception ex)
    {
        // Handle exception
        throw;
    }
}",
                    Tags = new List<string> { "async", "method", "template" }
                },
                new CodeSnippet
                {
                    Title = "React Functional Component",
                    Description = "React functional component with hooks",
                    Language = "JavaScript",
                    Category = "React",
                    Code = @"import React, { useState, useEffect } from 'react';

const ComponentName = ({ props }) => {
    const [state, setState] = useState(initialState);
    
    useEffect(() => {
        // Side effect
        return () => {
            // Cleanup
        };
    }, []);
    
    return (
        <div>
            {/* Component content */}
        </div>
    );
};

export default ComponentName;",
                    Tags = new List<string> { "react", "component", "hooks" }
                },
                new CodeSnippet
                {
                    Title = "Python Class Template",
                    Description = "Basic Python class structure",
                    Language = "Python",
                    Category = "Classes",
                    Code = @"class ClassName:
    """"""Class description""""""
    
    def __init__(self, param1, param2):
        """"""Initialize the class""""""
        self.param1 = param1
        self.param2 = param2
    
    def method_name(self):
        """"""Method description""""""
        pass
    
    def __str__(self):
        """"""String representation""""""
        return f""ClassName(param1={self.param1}, param2={self.param2})""",
                    Tags = new List<string> { "python", "class", "oop" }
                }
            };
            
            foreach (var snippet in defaultSnippets)
            {
                Task.Run(() => AddSnippetAsync(snippet));
            }
        }

        /// <summary>
        /// Categorizes a snippet
        /// </summary>
        private void CategorizeSnippet(CodeSnippet snippet)
        {
            if (!_categorizedSnippets.ContainsKey(snippet.Category))
            {
                _categorizedSnippets[snippet.Category] = new List<CodeSnippet>();
            }
            
            _categorizedSnippets[snippet.Category].Add(snippet);
        }

        /// <summary>
        /// Recategorizes all snippets
        /// </summary>
        private void RecategorizeSnippets()
        {
            _categorizedSnippets.Clear();
            
            foreach (var snippet in _snippets)
            {
                CategorizeSnippet(snippet);
            }
        }

        /// <summary>
        /// Extracts template variables from content
        /// </summary>
        private List<TemplateVariable> ExtractTemplateVariables(string content)
        {
            var variables = new List<TemplateVariable>();
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"\{\{(\w+)\}\}");
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var varName = match.Groups[1].Value;
                if (!variables.Any(v => v.Name == varName))
                {
                    variables.Add(new TemplateVariable
                    {
                        Name = varName,
                        Description = $"Variable: {varName}",
                        DefaultValue = string.Empty
                    });
                }
            }
            
            return variables;
        }

        /// <summary>
        /// Generates markdown export
        /// </summary>
        private string GenerateMarkdownExport()
        {
            var markdown = new System.Text.StringBuilder();
            
            markdown.AppendLine("# Code Snippets");
            markdown.AppendLine();
            
            foreach (var category in _categorizedSnippets)
            {
                markdown.AppendLine($"## {category.Key}");
                markdown.AppendLine();
                
                foreach (var snippet in category.Value)
                {
                    markdown.AppendLine($"### {snippet.Title}");
                    markdown.AppendLine();
                    markdown.AppendLine($"**Description:** {snippet.Description}");
                    markdown.AppendLine($"**Language:** {snippet.Language}");
                    markdown.AppendLine($"**Tags:** {string.Join(", ", snippet.Tags)}");
                    markdown.AppendLine();
                    markdown.AppendLine("```" + snippet.Language.ToLower());
                    markdown.AppendLine(snippet.Code);
                    markdown.AppendLine("```");
                    markdown.AppendLine();
                }
            }
            
            return markdown.ToString();
        }

        /// <summary>
        /// Converts snippets to VS Code format
        /// </summary>
        private string ConvertToVSCodeFormat()
        {
            var vscodeSnippets = new Dictionary<string, object>();
            
            foreach (var snippet in _snippets)
            {
                vscodeSnippets[snippet.Title] = new
                {
                    prefix = snippet.Title.ToLower().Replace(" ", "-"),
                    body = snippet.Code.Split('\n'),
                    description = snippet.Description
                };
            }
            
            return JsonSerializer.Serialize(vscodeSnippets, _jsonOptions);
        }
    }

    /// <summary>
    /// Code snippet model
    /// </summary>
    public class CodeSnippet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public List<string> Tags { get; set; } = new();
        public bool IsFavorite { get; set; }
        public int UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime? LastUsed { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Code template model
    /// </summary>
    public class CodeTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<TemplateVariable> Variables { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Template variable
    /// </summary>
    public class TemplateVariable
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public string? ValidationRegex { get; set; }
        public List<string>? AllowedValues { get; set; }
    }

    /// <summary>
    /// Search options for snippets
    /// </summary>
    public class SearchOptions
    {
        public string? Language { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public bool? IsFavorite { get; set; }
        public SortBy SortBy { get; set; } = SortBy.Modified;
        public bool SortDescending { get; set; } = true;
    }

    /// <summary>
    /// Sort options
    /// </summary>
    public enum SortBy
    {
        Title,
        Created,
        Modified,
        Usage
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum ExportFormat
    {
        Json,
        Markdown,
        VSCode
    }

    /// <summary>
    /// Snippet statistics
    /// </summary>
    public class SnippetStatistics
    {
        public int TotalSnippets { get; set; }
        public int TotalTemplates { get; set; }
        public int FavoriteCount { get; set; }
        public Dictionary<string, int> LanguageDistribution { get; set; } = new();
        public Dictionary<string, int> CategoryDistribution { get; set; } = new();
        public List<CodeSnippet> MostUsedSnippets { get; set; } = new();
        public List<CodeSnippet> RecentlyUsedSnippets { get; set; } = new();
    }

    /// <summary>
    /// Snippet event arguments
    /// </summary>
    public class SnippetEventArgs : EventArgs
    {
        public CodeSnippet Snippet { get; }
        
        public SnippetEventArgs(CodeSnippet snippet)
        {
            Snippet = snippet;
        }
    }

    /// <summary>
    /// Template event arguments
    /// </summary>
    public class TemplateEventArgs : EventArgs
    {
        public CodeTemplate Template { get; }
        
        public TemplateEventArgs(CodeTemplate template)
        {
            Template = template;
        }
    }
}