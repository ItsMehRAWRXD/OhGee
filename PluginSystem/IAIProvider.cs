using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KimiAppNative.PluginSystem
{
    /// <summary>
    /// Interface for AI provider plugins
    /// </summary>
    public interface IAIProvider : IPlugin
    {
        /// <summary>
        /// Available models for this provider
        /// </summary>
        List<AIModel> AvailableModels { get; }
        
        /// <summary>
        /// Currently selected model
        /// </summary>
        AIModel? CurrentModel { get; set; }
        
        /// <summary>
        /// Provider-specific settings
        /// </summary>
        AIProviderSettings Settings { get; set; }
        
        /// <summary>
        /// Sends a chat completion request
        /// </summary>
        Task<AIResponse> SendChatCompletionAsync(
            AIRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Sends a streaming chat completion request
        /// </summary>
        IAsyncEnumerable<AIStreamResponse> SendChatCompletionStreamAsync(
            AIRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates API credentials
        /// </summary>
        Task<bool> ValidateCredentialsAsync();
        
        /// <summary>
        /// Gets usage statistics
        /// </summary>
        Task<AIUsageStatistics> GetUsageStatisticsAsync();
        
        /// <summary>
        /// Estimates token count for text
        /// </summary>
        int EstimateTokenCount(string text);
    }
    
    /// <summary>
    /// AI model information
    /// </summary>
    public class AIModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public decimal PricePerThousandTokens { get; set; }
        public ModelCapabilities Capabilities { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// Model capabilities
    /// </summary>
    [Flags]
    public enum ModelCapabilities
    {
        None = 0,
        Chat = 1,
        Completion = 2,
        FunctionCalling = 4,
        Vision = 8,
        Audio = 16,
        FineTuning = 32,
        Embeddings = 64
    }
    
    /// <summary>
    /// AI provider settings
    /// </summary>
    public class AIProviderSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string? ApiEndpoint { get; set; }
        public string? OrganizationId { get; set; }
        public int MaxRetries { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }
    
    /// <summary>
    /// AI request
    /// </summary>
    public class AIRequest
    {
        public List<AIMessage> Messages { get; set; } = new();
        public double Temperature { get; set; } = 0.7;
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public double? FrequencyPenalty { get; set; }
        public double? PresencePenalty { get; set; }
        public List<string>? StopSequences { get; set; }
        public Dictionary<string, object>? Functions { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// AI message
    /// </summary>
    public class AIMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
        public string? Name { get; set; }
        public Dictionary<string, object>? FunctionCall { get; set; }
    }
    
    /// <summary>
    /// AI response
    /// </summary>
    public class AIResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<AIChoice> Choices { get; set; } = new();
        public AIUsage? Usage { get; set; }
        public DateTime Created { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// AI choice in response
    /// </summary>
    public class AIChoice
    {
        public int Index { get; set; }
        public AIMessage Message { get; set; } = new();
        public string? FinishReason { get; set; }
    }
    
    /// <summary>
    /// AI streaming response
    /// </summary>
    public class AIStreamResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public AIStreamChoice Choice { get; set; } = new();
        public DateTime Created { get; set; }
    }
    
    /// <summary>
    /// AI streaming choice
    /// </summary>
    public class AIStreamChoice
    {
        public int Index { get; set; }
        public AIStreamDelta Delta { get; set; } = new();
        public string? FinishReason { get; set; }
    }
    
    /// <summary>
    /// AI streaming delta
    /// </summary>
    public class AIStreamDelta
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public Dictionary<string, object>? FunctionCall { get; set; }
    }
    
    /// <summary>
    /// AI usage information
    /// </summary>
    public class AIUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal? Cost { get; set; }
    }
    
    /// <summary>
    /// AI usage statistics
    /// </summary>
    public class AIUsageStatistics
    {
        public int TotalRequests { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public Dictionary<string, int> ModelUsage { get; set; } = new();
    }
}