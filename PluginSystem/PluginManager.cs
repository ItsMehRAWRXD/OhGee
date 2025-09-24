using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.Json;

namespace KimiAppNative.PluginSystem
{
    /// <summary>
    /// Manages plugin loading, lifecycle, and interactions
    /// </summary>
    public class PluginManager
    {
        private readonly Dictionary<string, IPlugin> _loadedPlugins;
        private readonly Dictionary<string, PluginInfo> _pluginRegistry;
        private readonly string _pluginsDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public IReadOnlyDictionary<string, IPlugin> LoadedPlugins => _loadedPlugins;
        public IReadOnlyDictionary<string, PluginInfo> PluginRegistry => _pluginRegistry;

        public event EventHandler<PluginEventArgs>? PluginLoaded;
        public event EventHandler<PluginEventArgs>? PluginUnloaded;
        public event EventHandler<PluginErrorEventArgs>? PluginError;
        public event EventHandler? PluginsRefreshed;

        public PluginManager()
        {
            _loadedPlugins = new Dictionary<string, IPlugin>();
            _pluginRegistry = new Dictionary<string, PluginInfo>();
            
            _pluginsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OhGee",
                "Plugins"
            );
            
            Directory.CreateDirectory(_pluginsDirectory);
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            
            // Create built-in plugins directory
            var builtInDir = Path.Combine(_pluginsDirectory, "BuiltIn");
            Directory.CreateDirectory(builtInDir);
        }

        /// <summary>
        /// Discovers and registers all available plugins
        /// </summary>
        public async Task DiscoverPluginsAsync()
        {
            _pluginRegistry.Clear();
            
            // Discover built-in plugins
            await DiscoverBuiltInPluginsAsync();
            
            // Discover external plugins
            await DiscoverExternalPluginsAsync();
            
            PluginsRefreshed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Loads a specific plugin
        /// </summary>
        public async Task<bool> LoadPluginAsync(string pluginId)
        {
            try
            {
                if (_loadedPlugins.ContainsKey(pluginId))
                {
                    Console.WriteLine($"Plugin {pluginId} is already loaded");
                    return true;
                }

                if (!_pluginRegistry.TryGetValue(pluginId, out var pluginInfo))
                {
                    Console.WriteLine($"Plugin {pluginId} not found in registry");
                    return false;
                }

                IPlugin? plugin = null;

                // Load based on plugin type
                if (pluginInfo.IsBuiltIn)
                {
                    plugin = LoadBuiltInPlugin(pluginInfo);
                }
                else
                {
                    plugin = await LoadExternalPluginAsync(pluginInfo);
                }

                if (plugin == null)
                {
                    Console.WriteLine($"Failed to instantiate plugin {pluginId}");
                    return false;
                }

                // Initialize the plugin
                var initialized = await plugin.InitializeAsync();
                if (!initialized)
                {
                    Console.WriteLine($"Plugin {pluginId} failed to initialize");
                    return false;
                }

                _loadedPlugins[pluginId] = plugin;
                PluginLoaded?.Invoke(this, new PluginEventArgs(plugin));
                
                Console.WriteLine($"Successfully loaded plugin: {plugin.Name}");
                return true;
            }
            catch (Exception ex)
            {
                var error = new PluginErrorEventArgs(pluginId, ex);
                PluginError?.Invoke(this, error);
                Console.WriteLine($"Error loading plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unloads a specific plugin
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
                {
                    Console.WriteLine($"Plugin {pluginId} is not loaded");
                    return false;
                }

                await plugin.ShutdownAsync();
                _loadedPlugins.Remove(pluginId);
                
                PluginUnloaded?.Invoke(this, new PluginEventArgs(plugin));
                Console.WriteLine($"Successfully unloaded plugin: {plugin.Name}");
                return true;
            }
            catch (Exception ex)
            {
                var error = new PluginErrorEventArgs(pluginId, ex);
                PluginError?.Invoke(this, error);
                Console.WriteLine($"Error unloading plugin {pluginId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a loaded plugin by ID
        /// </summary>
        public T? GetPlugin<T>(string pluginId) where T : class, IPlugin
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                return plugin as T;
            }
            return null;
        }

        /// <summary>
        /// Gets all loaded plugins of a specific type
        /// </summary>
        public List<T> GetPluginsOfType<T>() where T : class, IPlugin
        {
            return _loadedPlugins.Values
                .OfType<T>()
                .ToList();
        }

        /// <summary>
        /// Gets all loaded AI provider plugins
        /// </summary>
        public List<IAIProvider> GetAIProviders()
        {
            return GetPluginsOfType<IAIProvider>();
        }

        /// <summary>
        /// Reloads a plugin
        /// </summary>
        public async Task<bool> ReloadPluginAsync(string pluginId)
        {
            await UnloadPluginAsync(pluginId);
            return await LoadPluginAsync(pluginId);
        }

        /// <summary>
        /// Loads all auto-load plugins
        /// </summary>
        public async Task LoadAutoLoadPluginsAsync()
        {
            var autoLoadPlugins = _pluginRegistry.Values
                .Where(p => p.AutoLoad)
                .ToList();

            foreach (var pluginInfo in autoLoadPlugins)
            {
                await LoadPluginAsync(pluginInfo.Id);
            }
        }

        /// <summary>
        /// Installs a plugin from a package file
        /// </summary>
        public async Task<bool> InstallPluginAsync(string packagePath)
        {
            try
            {
                var fileName = Path.GetFileName(packagePath);
                var pluginDir = Path.Combine(_pluginsDirectory, Path.GetFileNameWithoutExtension(fileName));
                
                Directory.CreateDirectory(pluginDir);
                
                // Extract plugin files (assuming it's a zip file)
                System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, pluginDir, true);
                
                // Load plugin manifest
                var manifestPath = Path.Combine(pluginDir, "plugin.json");
                if (!File.Exists(manifestPath))
                {
                    Console.WriteLine("Plugin manifest not found");
                    Directory.Delete(pluginDir, true);
                    return false;
                }
                
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, _jsonOptions);
                
                if (manifest == null)
                {
                    Console.WriteLine("Invalid plugin manifest");
                    Directory.Delete(pluginDir, true);
                    return false;
                }
                
                // Register the plugin
                var pluginInfo = new PluginInfo
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Description = manifest.Description,
                    Version = manifest.Version,
                    Author = manifest.Author,
                    AssemblyPath = Path.Combine(pluginDir, manifest.AssemblyFile),
                    ClassName = manifest.ClassName,
                    IsBuiltIn = false,
                    AutoLoad = manifest.AutoLoad
                };
                
                _pluginRegistry[pluginInfo.Id] = pluginInfo;
                
                // Save registry
                await SavePluginRegistryAsync();
                
                Console.WriteLine($"Successfully installed plugin: {manifest.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing plugin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uninstalls a plugin
        /// </summary>
        public async Task<bool> UninstallPluginAsync(string pluginId)
        {
            try
            {
                // Unload if loaded
                await UnloadPluginAsync(pluginId);
                
                if (!_pluginRegistry.TryGetValue(pluginId, out var pluginInfo))
                {
                    return false;
                }
                
                if (pluginInfo.IsBuiltIn)
                {
                    Console.WriteLine("Cannot uninstall built-in plugins");
                    return false;
                }
                
                // Delete plugin files
                var pluginDir = Path.GetDirectoryName(pluginInfo.AssemblyPath);
                if (!string.IsNullOrEmpty(pluginDir) && Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, true);
                }
                
                // Remove from registry
                _pluginRegistry.Remove(pluginId);
                await SavePluginRegistryAsync();
                
                Console.WriteLine($"Successfully uninstalled plugin: {pluginId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uninstalling plugin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Discovers built-in plugins
        /// </summary>
        private async Task DiscoverBuiltInPluginsAsync()
        {
            // Register built-in OpenAI provider
            _pluginRegistry["openai"] = new PluginInfo
            {
                Id = "openai",
                Name = "OpenAI Provider",
                Description = "OpenAI ChatGPT models",
                Version = "1.0.0",
                Author = "OhGee",
                ClassName = "KimiAppNative.PluginSystem.Providers.OpenAIProvider",
                IsBuiltIn = true,
                AutoLoad = true
            };
            
            // Register built-in Moonshot provider
            _pluginRegistry["moonshot"] = new PluginInfo
            {
                Id = "moonshot",
                Name = "Moonshot Provider",
                Description = "Moonshot Kimi AI models",
                Version = "1.0.0",
                Author = "OhGee",
                ClassName = "KimiAppNative.PluginSystem.Providers.MoonshotProvider",
                IsBuiltIn = true,
                AutoLoad = true
            };
            
            // Register built-in DeepSeek provider
            _pluginRegistry["deepseek"] = new PluginInfo
            {
                Id = "deepseek",
                Name = "DeepSeek Provider",
                Description = "DeepSeek AI models",
                Version = "1.0.0",
                Author = "OhGee",
                ClassName = "KimiAppNative.PluginSystem.Providers.DeepSeekProvider",
                IsBuiltIn = true,
                AutoLoad = true
            };
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Discovers external plugins
        /// </summary>
        private async Task DiscoverExternalPluginsAsync()
        {
            var pluginDirs = Directory.GetDirectories(_pluginsDirectory)
                .Where(d => !d.EndsWith("BuiltIn"));
            
            foreach (var pluginDir in pluginDirs)
            {
                try
                {
                    var manifestPath = Path.Combine(pluginDir, "plugin.json");
                    if (!File.Exists(manifestPath))
                        continue;
                    
                    var manifestJson = await File.ReadAllTextAsync(manifestPath);
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, _jsonOptions);
                    
                    if (manifest != null)
                    {
                        var pluginInfo = new PluginInfo
                        {
                            Id = manifest.Id,
                            Name = manifest.Name,
                            Description = manifest.Description,
                            Version = manifest.Version,
                            Author = manifest.Author,
                            AssemblyPath = Path.Combine(pluginDir, manifest.AssemblyFile),
                            ClassName = manifest.ClassName,
                            IsBuiltIn = false,
                            AutoLoad = manifest.AutoLoad
                        };
                        
                        _pluginRegistry[pluginInfo.Id] = pluginInfo;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error discovering plugin in {pluginDir}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Loads a built-in plugin
        /// </summary>
        private IPlugin? LoadBuiltInPlugin(PluginInfo pluginInfo)
        {
            try
            {
                var type = Type.GetType(pluginInfo.ClassName);
                if (type == null)
                {
                    Console.WriteLine($"Type {pluginInfo.ClassName} not found");
                    return null;
                }
                
                return Activator.CreateInstance(type) as IPlugin;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading built-in plugin: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads an external plugin from assembly
        /// </summary>
        private async Task<IPlugin?> LoadExternalPluginAsync(PluginInfo pluginInfo)
        {
            try
            {
                if (!File.Exists(pluginInfo.AssemblyPath))
                {
                    Console.WriteLine($"Plugin assembly not found: {pluginInfo.AssemblyPath}");
                    return null;
                }
                
                var assembly = Assembly.LoadFrom(pluginInfo.AssemblyPath);
                var type = assembly.GetType(pluginInfo.ClassName);
                
                if (type == null)
                {
                    Console.WriteLine($"Type {pluginInfo.ClassName} not found in assembly");
                    return null;
                }
                
                return Activator.CreateInstance(type) as IPlugin;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading external plugin: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves the plugin registry
        /// </summary>
        private async Task SavePluginRegistryAsync()
        {
            var registryPath = Path.Combine(_pluginsDirectory, "registry.json");
            var registryData = _pluginRegistry.Values
                .Where(p => !p.IsBuiltIn)
                .ToList();
            
            var json = JsonSerializer.Serialize(registryData, _jsonOptions);
            await File.WriteAllTextAsync(registryPath, json);
        }
    }

    /// <summary>
    /// Plugin information
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string AssemblyPath { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public bool IsBuiltIn { get; set; }
        public bool AutoLoad { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Plugin manifest
    /// </summary>
    public class PluginManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string AssemblyFile { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public bool AutoLoad { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Plugin event arguments
    /// </summary>
    public class PluginEventArgs : EventArgs
    {
        public IPlugin Plugin { get; }
        
        public PluginEventArgs(IPlugin plugin)
        {
            Plugin = plugin;
        }
    }

    /// <summary>
    /// Plugin error event arguments
    /// </summary>
    public class PluginErrorEventArgs : EventArgs
    {
        public string PluginId { get; }
        public Exception Exception { get; }
        
        public PluginErrorEventArgs(string pluginId, Exception exception)
        {
            PluginId = pluginId;
            Exception = exception;
        }
    }
}