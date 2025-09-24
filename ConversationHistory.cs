using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KimiAppNative
{
    /// <summary>
    /// Represents a single message in a conversation
    /// </summary>
    public class ConversationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Role { get; set; } = "user"; // "user", "assistant", "system"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Model { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents a conversation session
    /// </summary>
    public class ConversationSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "New Conversation";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<ConversationMessage> Messages { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public Dictionary<string, object> Settings { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public bool IsPinned { get; set; } = false;
        public bool IsArchived { get; set; } = false;
    }

    /// <summary>
    /// Manages conversation history with persistence
    /// </summary>
    public class ConversationHistoryManager
    {
        private readonly string _historyPath;
        private readonly ObservableCollection<ConversationSession> _sessions;
        private ConversationSession? _currentSession;
        private readonly int _maxSessionsInMemory = 50;
        private readonly JsonSerializerOptions _jsonOptions;

        public ObservableCollection<ConversationSession> Sessions => _sessions;
        public ConversationSession? CurrentSession => _currentSession;

        public event EventHandler<ConversationSession>? SessionChanged;
        public event EventHandler<ConversationMessage>? MessageAdded;
        public event EventHandler? HistoryUpdated;

        public ConversationHistoryManager()
        {
            _historyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OhGee",
                "ConversationHistory"
            );

            Directory.CreateDirectory(_historyPath);
            _sessions = new ObservableCollection<ConversationSession>();
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            LoadRecentSessions();
        }

        /// <summary>
        /// Creates a new conversation session
        /// </summary>
        public ConversationSession CreateNewSession(string title = "New Conversation", string model = "")
        {
            var session = new ConversationSession
            {
                Title = title,
                Model = model,
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            _sessions.Insert(0, session);
            _currentSession = session;
            
            SessionChanged?.Invoke(this, session);
            SaveSession(session);
            
            // Trim old sessions if needed
            TrimSessions();
            
            return session;
        }

        /// <summary>
        /// Adds a message to the current session
        /// </summary>
        public async Task<ConversationMessage> AddMessageAsync(string role, string content, string model = "")
        {
            if (_currentSession == null)
            {
                _currentSession = CreateNewSession();
            }

            var message = new ConversationMessage
            {
                Role = role,
                Content = content,
                Model = model,
                Timestamp = DateTime.Now
            };

            _currentSession.Messages.Add(message);
            _currentSession.LastModified = DateTime.Now;
            
            // Auto-generate title from first user message if needed
            if (_currentSession.Title == "New Conversation" && 
                role == "user" && 
                _currentSession.Messages.Count == 1)
            {
                _currentSession.Title = GenerateTitle(content);
            }

            MessageAdded?.Invoke(this, message);
            await SaveSessionAsync(_currentSession);
            
            return message;
        }

        /// <summary>
        /// Loads a specific session
        /// </summary>
        public async Task<ConversationSession?> LoadSessionAsync(string sessionId)
        {
            var sessionFile = Path.Combine(_historyPath, $"{sessionId}.json");
            
            if (!File.Exists(sessionFile))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(sessionFile);
                var session = JsonSerializer.Deserialize<ConversationSession>(json, _jsonOptions);
                
                if (session != null)
                {
                    _currentSession = session;
                    
                    // Add to memory if not already there
                    if (!_sessions.Any(s => s.Id == session.Id))
                    {
                        _sessions.Insert(0, session);
                        TrimSessions();
                    }
                    
                    SessionChanged?.Invoke(this, session);
                }
                
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading session {sessionId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves a session to disk
        /// </summary>
        public async Task SaveSessionAsync(ConversationSession session)
        {
            var sessionFile = Path.Combine(_historyPath, $"{session.Id}.json");
            
            try
            {
                var json = JsonSerializer.Serialize(session, _jsonOptions);
                await File.WriteAllTextAsync(sessionFile, json);
                HistoryUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving session {session.Id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Synchronous save for immediate persistence
        /// </summary>
        private void SaveSession(ConversationSession session)
        {
            Task.Run(() => SaveSessionAsync(session));
        }

        /// <summary>
        /// Searches conversations
        /// </summary>
        public async Task<List<ConversationSession>> SearchSessionsAsync(string query)
        {
            var results = new List<ConversationSession>();
            var files = Directory.GetFiles(_historyPath, "*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var session = JsonSerializer.Deserialize<ConversationSession>(json, _jsonOptions);
                    
                    if (session != null && MatchesQuery(session, query))
                    {
                        results.Add(session);
                    }
                }
                catch
                {
                    // Skip corrupted files
                }
            }
            
            return results.OrderByDescending(s => s.LastModified).ToList();
        }

        /// <summary>
        /// Deletes a session
        /// </summary>
        public async Task DeleteSessionAsync(string sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                _sessions.Remove(session);
            }
            
            var sessionFile = Path.Combine(_historyPath, $"{sessionId}.json");
            if (File.Exists(sessionFile))
            {
                await Task.Run(() => File.Delete(sessionFile));
            }
            
            if (_currentSession?.Id == sessionId)
            {
                _currentSession = null;
                SessionChanged?.Invoke(this, null!);
            }
            
            HistoryUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Archives a session
        /// </summary>
        public async Task ArchiveSessionAsync(string sessionId)
        {
            var session = await LoadSessionAsync(sessionId);
            if (session != null)
            {
                session.IsArchived = true;
                await SaveSessionAsync(session);
                
                // Remove from active sessions
                var activeSession = _sessions.FirstOrDefault(s => s.Id == sessionId);
                if (activeSession != null)
                {
                    _sessions.Remove(activeSession);
                }
            }
        }

        /// <summary>
        /// Pins/unpins a session
        /// </summary>
        public async Task TogglePinSessionAsync(string sessionId)
        {
            var session = await LoadSessionAsync(sessionId);
            if (session != null)
            {
                session.IsPinned = !session.IsPinned;
                await SaveSessionAsync(session);
                
                // Reorder sessions with pinned at top
                ReorderSessions();
            }
        }

        /// <summary>
        /// Exports conversation to various formats
        /// </summary>
        public async Task<string> ExportSessionAsync(string sessionId, ExportFormat format)
        {
            var session = await LoadSessionAsync(sessionId);
            if (session == null)
                return string.Empty;

            return format switch
            {
                ExportFormat.Json => JsonSerializer.Serialize(session, _jsonOptions),
                ExportFormat.Markdown => ExportToMarkdown(session),
                ExportFormat.Text => ExportToText(session),
                ExportFormat.Html => ExportToHtml(session),
                _ => string.Empty
            };
        }

        /// <summary>
        /// Gets statistics about conversations
        /// </summary>
        public async Task<ConversationStatistics> GetStatisticsAsync()
        {
            var stats = new ConversationStatistics();
            var files = Directory.GetFiles(_historyPath, "*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var session = JsonSerializer.Deserialize<ConversationSession>(json, _jsonOptions);
                    
                    if (session != null)
                    {
                        stats.TotalConversations++;
                        stats.TotalMessages += session.Messages.Count;
                        
                        if (!string.IsNullOrEmpty(session.Model))
                        {
                            if (stats.ModelUsage.ContainsKey(session.Model))
                                stats.ModelUsage[session.Model]++;
                            else
                                stats.ModelUsage[session.Model] = 1;
                        }
                        
                        if (session.CreatedAt > DateTime.Now.AddDays(-7))
                            stats.ConversationsThisWeek++;
                        
                        if (session.CreatedAt > DateTime.Now.AddDays(-30))
                            stats.ConversationsThisMonth++;
                    }
                }
                catch
                {
                    // Skip corrupted files
                }
            }
            
            return stats;
        }

        /// <summary>
        /// Loads recent sessions into memory
        /// </summary>
        private void LoadRecentSessions()
        {
            try
            {
                var files = Directory.GetFiles(_historyPath, "*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(_maxSessionsInMemory);

                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file.FullName);
                        var session = JsonSerializer.Deserialize<ConversationSession>(json, _jsonOptions);
                        
                        if (session != null && !session.IsArchived)
                        {
                            _sessions.Add(session);
                        }
                    }
                    catch
                    {
                        // Skip corrupted files
                    }
                }
                
                ReorderSessions();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading recent sessions: {ex.Message}");
            }
        }

        /// <summary>
        /// Trims sessions to maintain memory limit
        /// </summary>
        private void TrimSessions()
        {
            while (_sessions.Count > _maxSessionsInMemory)
            {
                var oldestUnpinned = _sessions
                    .Where(s => !s.IsPinned)
                    .OrderBy(s => s.LastModified)
                    .FirstOrDefault();
                
                if (oldestUnpinned != null)
                {
                    _sessions.Remove(oldestUnpinned);
                }
                else
                {
                    break; // All remaining are pinned
                }
            }
        }

        /// <summary>
        /// Reorders sessions with pinned at top
        /// </summary>
        private void ReorderSessions()
        {
            var ordered = _sessions
                .OrderByDescending(s => s.IsPinned)
                .ThenByDescending(s => s.LastModified)
                .ToList();
            
            _sessions.Clear();
            foreach (var session in ordered)
            {
                _sessions.Add(session);
            }
        }

        /// <summary>
        /// Generates a title from content
        /// </summary>
        private string GenerateTitle(string content)
        {
            var title = content.Length > 50 
                ? content.Substring(0, 47) + "..." 
                : content;
            
            // Clean up title
            title = title.Replace("\n", " ").Replace("\r", " ").Trim();
            
            return string.IsNullOrWhiteSpace(title) ? "New Conversation" : title;
        }

        /// <summary>
        /// Checks if session matches search query
        /// </summary>
        private bool MatchesQuery(ConversationSession session, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            
            query = query.ToLower();
            
            // Check title
            if (session.Title.ToLower().Contains(query))
                return true;
            
            // Check messages
            if (session.Messages.Any(m => m.Content.ToLower().Contains(query)))
                return true;
            
            // Check tags
            if (session.Tags.Any(t => t.ToLower().Contains(query)))
                return true;
            
            return false;
        }

        /// <summary>
        /// Exports session to Markdown format
        /// </summary>
        private string ExportToMarkdown(ConversationSession session)
        {
            var markdown = new System.Text.StringBuilder();
            
            markdown.AppendLine($"# {session.Title}");
            markdown.AppendLine();
            markdown.AppendLine($"**Created:** {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine($"**Last Modified:** {session.LastModified:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine($"**Model:** {session.Model}");
            
            if (session.Tags.Any())
            {
                markdown.AppendLine($"**Tags:** {string.Join(", ", session.Tags)}");
            }
            
            markdown.AppendLine();
            markdown.AppendLine("---");
            markdown.AppendLine();
            
            foreach (var message in session.Messages)
            {
                var role = message.Role == "user" ? "👤 User" : "🤖 Assistant";
                markdown.AppendLine($"### {role}");
                markdown.AppendLine($"*{message.Timestamp:yyyy-MM-dd HH:mm:ss}*");
                markdown.AppendLine();
                markdown.AppendLine(message.Content);
                markdown.AppendLine();
            }
            
            return markdown.ToString();
        }

        /// <summary>
        /// Exports session to plain text format
        /// </summary>
        private string ExportToText(ConversationSession session)
        {
            var text = new System.Text.StringBuilder();
            
            text.AppendLine($"CONVERSATION: {session.Title}");
            text.AppendLine($"Created: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"Model: {session.Model}");
            text.AppendLine(new string('=', 50));
            text.AppendLine();
            
            foreach (var message in session.Messages)
            {
                text.AppendLine($"[{message.Role.ToUpper()}] {message.Timestamp:yyyy-MM-dd HH:mm:ss}");
                text.AppendLine(message.Content);
                text.AppendLine(new string('-', 30));
                text.AppendLine();
            }
            
            return text.ToString();
        }

        /// <summary>
        /// Exports session to HTML format
        /// </summary>
        private string ExportToHtml(ConversationSession session)
        {
            var html = new System.Text.StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine($"<title>{session.Title}</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }");
            html.AppendLine(".message { margin: 20px 0; padding: 15px; border-radius: 8px; }");
            html.AppendLine(".user { background: #e3f2fd; }");
            html.AppendLine(".assistant { background: #f5f5f5; }");
            html.AppendLine(".role { font-weight: bold; color: #333; }");
            html.AppendLine(".timestamp { font-size: 0.9em; color: #666; }");
            html.AppendLine(".content { margin-top: 10px; white-space: pre-wrap; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine($"<h1>{session.Title}</h1>");
            html.AppendLine($"<p><strong>Created:</strong> {session.CreatedAt:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"<p><strong>Model:</strong> {session.Model}</p>");
            html.AppendLine("<hr>");
            
            foreach (var message in session.Messages)
            {
                var cssClass = message.Role == "user" ? "user" : "assistant";
                var roleDisplay = message.Role == "user" ? "User" : "Assistant";
                
                html.AppendLine($"<div class='message {cssClass}'>");
                html.AppendLine($"<div class='role'>{roleDisplay}</div>");
                html.AppendLine($"<div class='timestamp'>{message.Timestamp:yyyy-MM-dd HH:mm:ss}</div>");
                html.AppendLine($"<div class='content'>{System.Web.HttpUtility.HtmlEncode(message.Content)}</div>");
                html.AppendLine("</div>");
            }
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum ExportFormat
    {
        Json,
        Markdown,
        Text,
        Html
    }

    /// <summary>
    /// Conversation statistics
    /// </summary>
    public class ConversationStatistics
    {
        public int TotalConversations { get; set; }
        public int TotalMessages { get; set; }
        public int ConversationsThisWeek { get; set; }
        public int ConversationsThisMonth { get; set; }
        public Dictionary<string, int> ModelUsage { get; set; } = new();
    }
}