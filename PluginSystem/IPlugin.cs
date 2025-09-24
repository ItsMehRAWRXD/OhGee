using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KimiAppNative.PluginSystem
{
    /// <summary>
    /// Base interface for all plugins
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Unique identifier for the plugin
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Display name of the plugin
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Plugin description
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Plugin version
        /// </summary>
        Version Version { get; }
        
        /// <summary>
        /// Plugin author
        /// </summary>
        string Author { get; }
        
        /// <summary>
        /// Plugin website or documentation URL
        /// </summary>
        string? WebsiteUrl { get; }
        
        /// <summary>
        /// Plugin icon path or resource
        /// </summary>
        string? IconPath { get; }
        
        /// <summary>
        /// Plugin capabilities
        /// </summary>
        PluginCapabilities Capabilities { get; }
        
        /// <summary>
        /// Plugin configuration
        /// </summary>
        Dictionary<string, object> Configuration { get; set; }
        
        /// <summary>
        /// Initializes the plugin
        /// </summary>
        Task<bool> InitializeAsync();
        
        /// <summary>
        /// Shuts down the plugin
        /// </summary>
        Task ShutdownAsync();
        
        /// <summary>
        /// Validates plugin configuration
        /// </summary>
        bool ValidateConfiguration();
        
        /// <summary>
        /// Gets plugin status
        /// </summary>
        PluginStatus GetStatus();
    }
    
    /// <summary>
    /// Plugin capabilities flags
    /// </summary>
    [Flags]
    public enum PluginCapabilities
    {
        None = 0,
        ChatCompletion = 1,
        ImageGeneration = 2,
        CodeGeneration = 4,
        AudioTranscription = 8,
        Translation = 16,
        Embedding = 32,
        FileAnalysis = 64,
        WebSearch = 128,
        CustomUI = 256,
        BackgroundTask = 512
    }
    
    /// <summary>
    /// Plugin status
    /// </summary>
    public enum PluginStatus
    {
        NotInitialized,
        Initializing,
        Ready,
        Busy,
        Error,
        Disabled,
        ShuttingDown
    }
}