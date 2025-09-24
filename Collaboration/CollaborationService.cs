using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace KimiAppNative.Collaboration
{
    /// <summary>
    /// Manages collaborative features and sharing
    /// </summary>
    public class CollaborationService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private HubConnection? _hubConnection;
        private readonly string _baseUrl;
        private readonly Dictionary<string, SharedSession> _activeSessions;
        private readonly Dictionary<string, List<CollaborationUser>> _sessionUsers;
        private string? _currentUserId;
        private string? _currentSessionId;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public string? CurrentUserId => _currentUserId;
        public string? CurrentSessionId => _currentSessionId;
        public IReadOnlyDictionary<string, SharedSession> ActiveSessions => _activeSessions;

        public event EventHandler<CollaborationEventArgs>? UserJoined;
        public event EventHandler<CollaborationEventArgs>? UserLeft;
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<SessionUpdatedEventArgs>? SessionUpdated;
        public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;

        public CollaborationService(string baseUrl = "https://collab.ohgee.app")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _activeSessions = new Dictionary<string, SharedSession>();
            _sessionUsers = new Dictionary<string, List<CollaborationUser>>();
        }

        /// <summary>
        /// Initializes the collaboration service
        /// </summary>
        public async Task InitializeAsync(string userId, string userName)
        {
            _currentUserId = userId;
            
            // Build SignalR connection
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/collaborationHub")
                .WithAutomaticReconnect()
                .Build();
            
            // Setup event handlers
            SetupHubHandlers();
            
            // Connect to hub
            await _hubConnection.StartAsync();
            
            // Register user
            await _hubConnection.InvokeAsync("RegisterUser", userId, userName);
            
            ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs { IsConnected = true });
        }

        /// <summary>
        /// Creates a new shared session
        /// </summary>
        public async Task<SharedSession> CreateSessionAsync(SessionSettings settings)
        {
            var session = new SharedSession
            {
                Id = GenerateSessionId(),
                Name = settings.Name,
                Description = settings.Description,
                CreatedBy = _currentUserId!,
                CreatedAt = DateTime.UtcNow,
                Settings = settings,
                AccessCode = GenerateAccessCode(),
                IsPublic = settings.IsPublic
            };
            
            // Register session on server
            var response = await _httpClient.PostAsJsonAsync("api/sessions", session);
            response.EnsureSuccessStatusCode();
            
            // Join the session
            await JoinSessionAsync(session.Id, session.AccessCode);
            
            _activeSessions[session.Id] = session;
            
            return session;
        }

        /// <summary>
        /// Joins an existing session
        /// </summary>
        public async Task<bool> JoinSessionAsync(string sessionId, string? accessCode = null)
        {
            try
            {
                // Verify access
                var request = new JoinSessionRequest
                {
                    SessionId = sessionId,
                    UserId = _currentUserId!,
                    AccessCode = accessCode
                };
                
                var response = await _httpClient.PostAsJsonAsync("api/sessions/join", request);
                
                if (!response.IsSuccessStatusCode)
                    return false;
                
                // Join SignalR group
                await _hubConnection!.InvokeAsync("JoinSession", sessionId);
                
                _currentSessionId = sessionId;
                
                // Get session details
                var sessionResponse = await _httpClient.GetAsync($"api/sessions/{sessionId}");
                if (sessionResponse.IsSuccessStatusCode)
                {
                    var session = await sessionResponse.Content.ReadFromJsonAsync<SharedSession>();
                    if (session != null)
                    {
                        _activeSessions[sessionId] = session;
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Leaves the current session
        /// </summary>
        public async Task LeaveSessionAsync()
        {
            if (_currentSessionId == null)
                return;
            
            await _hubConnection!.InvokeAsync("LeaveSession", _currentSessionId);
            
            _activeSessions.Remove(_currentSessionId);
            _sessionUsers.Remove(_currentSessionId);
            _currentSessionId = null;
        }

        /// <summary>
        /// Sends a message to the current session
        /// </summary>
        public async Task SendMessageAsync(CollaborationMessage message)
        {
            if (_currentSessionId == null)
                throw new InvalidOperationException("Not in a session");
            
            message.SenderId = _currentUserId!;
            message.SessionId = _currentSessionId;
            message.Timestamp = DateTime.UtcNow;
            
            await _hubConnection!.InvokeAsync("SendMessage", _currentSessionId, message);
        }

        /// <summary>
        /// Shares content with the session
        /// </summary>
        public async Task<SharedContent> ShareContentAsync(string content, ContentType type, Dictionary<string, object>? metadata = null)
        {
            if (_currentSessionId == null)
                throw new InvalidOperationException("Not in a session");
            
            var sharedContent = new SharedContent
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _currentSessionId,
                SharedBy = _currentUserId!,
                Content = content,
                Type = type,
                Metadata = metadata ?? new Dictionary<string, object>(),
                SharedAt = DateTime.UtcNow
            };
            
            // Upload content
            var response = await _httpClient.PostAsJsonAsync("api/content", sharedContent);
            response.EnsureSuccessStatusCode();
            
            // Notify session members
            await _hubConnection!.InvokeAsync("ContentShared", _currentSessionId, sharedContent);
            
            return sharedContent;
        }

        /// <summary>
        /// Shares a file
        /// </summary>
        public async Task<SharedFile> ShareFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            if (_currentSessionId == null)
                throw new InvalidOperationException("Not in a session");
            
            var fileInfo = new FileInfo(filePath);
            var sharedFile = new SharedFile
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _currentSessionId,
                SharedBy = _currentUserId!,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                FileHash = await CalculateFileHashAsync(filePath),
                SharedAt = DateTime.UtcNow
            };
            
            // Upload file
            using var stream = File.OpenRead(filePath);
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(stream), "file", fileInfo.Name);
            content.Add(new StringContent(JsonSerializer.Serialize(sharedFile)), "metadata");
            
            var response = await _httpClient.PostAsync("api/files", content);
            response.EnsureSuccessStatusCode();
            
            // Notify session members
            await _hubConnection!.InvokeAsync("FileShared", _currentSessionId, sharedFile);
            
            return sharedFile;
        }

        /// <summary>
        /// Downloads a shared file
        /// </summary>
        public async Task<string> DownloadFileAsync(string fileId, string downloadPath)
        {
            var response = await _httpClient.GetAsync($"api/files/{fileId}");
            response.EnsureSuccessStatusCode();
            
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"{fileId}.dat";
            var fullPath = Path.Combine(downloadPath, fileName);
            
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(fullPath);
            await stream.CopyToAsync(fileStream);
            
            return fullPath;
        }

        /// <summary>
        /// Starts screen sharing
        /// </summary>
        public async Task StartScreenSharingAsync()
        {
            if (_currentSessionId == null)
                throw new InvalidOperationException("Not in a session");
            
            // Implementation would involve screen capture and streaming
            await _hubConnection!.InvokeAsync("StartScreenShare", _currentSessionId);
        }

        /// <summary>
        /// Stops screen sharing
        /// </summary>
        public async Task StopScreenSharingAsync()
        {
            if (_currentSessionId == null)
                return;
            
            await _hubConnection!.InvokeAsync("StopScreenShare", _currentSessionId);
        }

        /// <summary>
        /// Gets session participants
        /// </summary>
        public async Task<List<CollaborationUser>> GetSessionParticipantsAsync(string sessionId)
        {
            var response = await _httpClient.GetAsync($"api/sessions/{sessionId}/participants");
            response.EnsureSuccessStatusCode();
            
            var participants = await response.Content.ReadFromJsonAsync<List<CollaborationUser>>();
            return participants ?? new List<CollaborationUser>();
        }

        /// <summary>
        /// Gets session history
        /// </summary>
        public async Task<SessionHistory> GetSessionHistoryAsync(string sessionId)
        {
            var response = await _httpClient.GetAsync($"api/sessions/{sessionId}/history");
            response.EnsureSuccessStatusCode();
            
            var history = await response.Content.ReadFromJsonAsync<SessionHistory>();
            return history ?? new SessionHistory();
        }

        /// <summary>
        /// Exports session data
        /// </summary>
        public async Task<byte[]> ExportSessionAsync(string sessionId, ExportOptions options)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/sessions/{sessionId}/export", options);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Sets up SignalR hub handlers
        /// </summary>
        private void SetupHubHandlers()
        {
            if (_hubConnection == null) return;
            
            _hubConnection.On<CollaborationUser>("UserJoined", user =>
            {
                if (_currentSessionId != null)
                {
                    if (!_sessionUsers.ContainsKey(_currentSessionId))
                        _sessionUsers[_currentSessionId] = new List<CollaborationUser>();
                    
                    _sessionUsers[_currentSessionId].Add(user);
                    UserJoined?.Invoke(this, new CollaborationEventArgs { User = user });
                }
            });
            
            _hubConnection.On<CollaborationUser>("UserLeft", user =>
            {
                if (_currentSessionId != null && _sessionUsers.ContainsKey(_currentSessionId))
                {
                    _sessionUsers[_currentSessionId].RemoveAll(u => u.Id == user.Id);
                    UserLeft?.Invoke(this, new CollaborationEventArgs { User = user });
                }
            });
            
            _hubConnection.On<CollaborationMessage>("ReceiveMessage", message =>
            {
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = message });
            });
            
            _hubConnection.On<SharedSession>("SessionUpdated", session =>
            {
                if (_activeSessions.ContainsKey(session.Id))
                {
                    _activeSessions[session.Id] = session;
                    SessionUpdated?.Invoke(this, new SessionUpdatedEventArgs { Session = session });
                }
            });
            
            _hubConnection.Reconnecting += error =>
            {
                ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs { IsConnected = false });
                return Task.CompletedTask;
            };
            
            _hubConnection.Reconnected += connectionId =>
            {
                ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs { IsConnected = true });
                return Task.CompletedTask;
            };
        }

        /// <summary>
        /// Generates a session ID
        /// </summary>
        private string GenerateSessionId()
        {
            return $"session_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Generates an access code
        /// </summary>
        private string GenerateAccessCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Calculates file hash
        /// </summary>
        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await Task.Run(() => sha256.ComputeHash(stream));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            _hubConnection?.DisposeAsync().Wait();
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Shared session
    /// </summary>
    public class SharedSession
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public SessionSettings Settings { get; set; } = new();
        public string AccessCode { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Session settings
    /// </summary>
    public class SessionSettings
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool AllowAnonymous { get; set; }
        public bool RecordSession { get; set; }
        public int MaxParticipants { get; set; } = 10;
        public TimeSpan? SessionTimeout { get; set; }
        public List<string> AllowedFeatures { get; set; } = new();
    }

    /// <summary>
    /// Collaboration user
    /// </summary>
    public class CollaborationUser
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public UserRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsOnline { get; set; }
    }

    /// <summary>
    /// User roles
    /// </summary>
    public enum UserRole
    {
        Viewer,
        Participant,
        Moderator,
        Owner
    }

    /// <summary>
    /// Collaboration message
    /// </summary>
    public class CollaborationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public MessageType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Message types
    /// </summary>
    public enum MessageType
    {
        Text,
        Code,
        Image,
        File,
        System
    }

    /// <summary>
    /// Shared content
    /// </summary>
    public class SharedContent
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string SharedBy { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ContentType Type { get; set; }
        public DateTime SharedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Content types
    /// </summary>
    public enum ContentType
    {
        Text,
        Code,
        Markdown,
        Json,
        Xml,
        Image,
        Document
    }

    /// <summary>
    /// Shared file
    /// </summary>
    public class SharedFile
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string SharedBy { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileHash { get; set; } = string.Empty;
        public DateTime SharedAt { get; set; }
    }

    /// <summary>
    /// Session history
    /// </summary>
    public class SessionHistory
    {
        public string SessionId { get; set; } = string.Empty;
        public List<CollaborationMessage> Messages { get; set; } = new();
        public List<SharedContent> SharedContent { get; set; } = new();
        public List<SharedFile> SharedFiles { get; set; } = new();
        public List<SessionEvent> Events { get; set; } = new();
    }

    /// <summary>
    /// Session event
    /// </summary>
    public class SessionEvent
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public EventType Type { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    /// <summary>
    /// Event types
    /// </summary>
    public enum EventType
    {
        UserJoined,
        UserLeft,
        MessageSent,
        ContentShared,
        FileShared,
        ScreenShareStarted,
        ScreenShareStopped
    }

    /// <summary>
    /// Join session request
    /// </summary>
    public class JoinSessionRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? AccessCode { get; set; }
    }

    /// <summary>
    /// Export options
    /// </summary>
    public class ExportOptions
    {
        public bool IncludeMessages { get; set; } = true;
        public bool IncludeFiles { get; set; } = true;
        public bool IncludeContent { get; set; } = true;
        public bool IncludeEvents { get; set; } = true;
        public ExportFormat Format { get; set; } = ExportFormat.Json;
    }

    /// <summary>
    /// Export formats
    /// </summary>
    public enum ExportFormat
    {
        Json,
        Pdf,
        Html,
        Markdown
    }

    // Event arguments classes
    public class CollaborationEventArgs : EventArgs
    {
        public CollaborationUser User { get; set; } = new();
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public CollaborationMessage Message { get; set; } = new();
    }

    public class SessionUpdatedEventArgs : EventArgs
    {
        public SharedSession Session { get; set; } = new();
    }

    public class ConnectionStateEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
    }
}