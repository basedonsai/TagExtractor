using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using OCRTool.Core.Configuration;

namespace OCRTool.Application
{
    /// <summary>
    /// Loads configuration from XML file and tessdata path.
    /// Provides configuration to other components.
    /// </summary>
    public class ConfigurationManager
    {
        private readonly string _assetsFolder;
        private readonly string _configFileName;
        private ExtractionConfig? _cachedConfig;
        private string? _tessdataPath;
        private readonly string _userConfigFolder;

        /// <summary>
        /// Creates a new ConfigurationManager
        /// </summary>
        /// <param name="assetsFolder">Path to the assets folder containing config files</param>
        /// <param name="configFileName">Name of the configuration XML file</param>
        public ConfigurationManager(string assetsFolder, string configFileName = "extraction_config.xml")
        {
            _assetsFolder = assetsFolder ?? throw new ArgumentNullException(nameof(assetsFolder));
            _configFileName = configFileName ?? throw new ArgumentNullException(nameof(configFileName));
            
            // Create user config folder in AppData (user-writable location)
            _userConfigFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OCRTool");
            Directory.CreateDirectory(_userConfigFolder);
        }

        /// <summary>
        /// Creates a ConfigurationManager with default paths
        /// Assumes the application is running from a directory with an "assets" subfolder
        /// </summary>
        public static ConfigurationManager CreateDefault()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var assetsFolder = FindAssetsFolder(baseDirectory);
            
            return new ConfigurationManager(assetsFolder);
        }
        
        /// <summary>
        /// Finds the assets folder by checking multiple possible locations
        /// </summary>
        private static string FindAssetsFolder(string baseDirectory)
        {
            // First check directly in the base directory
            var assetsFolder = Path.Combine(baseDirectory, "assets");
            if (Directory.Exists(assetsFolder))
            {
                return assetsFolder;
            }
            
            // Check parent directory (for when running from bin/Debug/net10.0-windows)
            var parentDir = Directory.GetParent(baseDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parentDir))
            {
                assetsFolder = Path.Combine(parentDir, "assets");
                if (Directory.Exists(assetsFolder))
                {
                    return assetsFolder;
                }
                
                // Check parent's parent (for nested bin folders)
                var grandparentDir = Directory.GetParent(parentDir)?.FullName;
                if (!string.IsNullOrEmpty(grandparentDir))
                {
                    assetsFolder = Path.Combine(grandparentDir, "assets");
                    if (Directory.Exists(assetsFolder))
                    {
                        return assetsFolder;
                    }
                }
            }
            
            // Check current working directory
            var currentDir = Directory.GetCurrentDirectory();
            assetsFolder = Path.Combine(currentDir, "assets");
            if (Directory.Exists(assetsFolder))
            {
                return assetsFolder;
            }
            
            // Check relative to OCRTool folder
            assetsFolder = Path.Combine(currentDir, "OCRTool", "assets");
            if (Directory.Exists(assetsFolder))
            {
                return assetsFolder;
            }
            
            // Return the base directory assets folder as fallback (will fail later if not found)
            return Path.Combine(baseDirectory, "assets");
        }

        /// <summary>
        /// Loads the extraction configuration from XML file
        /// Loads from application directory, with embedded resource fallback
        /// </summary>
        /// <returns>The loaded configuration</returns>
        public ExtractionConfig LoadConfiguration()
        {
            // Return cached config if available
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            var configPath = GetConfigFilePathWithFallback(_configFileName);
            
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Loading config from: {configPath}");
            
            if (!File.Exists(configPath))
            {
                // Try to extract from embedded resource and create default config
                configPath = ExtractDefaultConfigIfNeeded();
            }
            
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {configPath}");
            }

            try
            {
                var serializer = new XmlSerializer(typeof(ExtractionConfig));
                using var reader = new FileStream(configPath, FileMode.Open, FileAccess.Read);
                var config = serializer.Deserialize(reader) as ExtractionConfig;
                
                if (config == null)
                {
                    throw new InvalidOperationException("Configuration deserialization returned null");
                }

                _cachedConfig = config;
                
                // Debug: print loaded patterns
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Loaded {config.Patterns?.Count ?? 0} patterns");
                if (config.Patterns != null)
                {
                    foreach (var p in config.Patterns)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CONFIG] Pattern: {p.Name} = {p.Regex}");
                    }
                }
                
                // Validate configuration
                ValidateConfiguration(_cachedConfig);
                
                return _cachedConfig;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load configuration from {configPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the tessdata path for Tesseract OCR
        /// </summary>
        /// <returns>Path to the tessdata folder</returns>
        public string GetTessdataPath()
        {
            // Return cached path if available
            if (!string.IsNullOrEmpty(_tessdataPath))
            {
                return _tessdataPath;
            }

            // Try multiple locations for tessdata
            var searchPaths = new[]
            {
                // Relative to application base directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
                
                // Relative to current working directory
                Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
                
                // Project-level tessdata folder
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "tesseract", "tessdata"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "tesseract", "tessdata"),
                
                // Common installation paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "tesseract", "tessdata"),
                
                // Check in assets folder relative to base directory
                Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "", "assets", "tessdata")
            };

            foreach (var path in searchPaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (Directory.Exists(normalizedPath))
                {
                    _tessdataPath = normalizedPath;
                    return _tessdataPath;
                }
            }

            // Fallback to default location in assets
            _tessdataPath = Path.Combine(_assetsFolder, "tessdata");
            
            if (!Directory.Exists(_tessdataPath))
            {
                throw new DirectoryNotFoundException($"Tessdata directory not found. Searched in: {string.Join(", ", searchPaths)}");
            }
            
            return _tessdataPath;
        }

        /// <summary>
        /// Gets the assets folder path
        /// </summary>
        public string GetAssetsFolder()
        {
            return _assetsFolder;
        }

        /// <summary>
        /// Gets the full path to a specific configuration file
        /// </summary>
        public string GetConfigFilePath(string fileName)
        {
            return Path.Combine(_assetsFolder, fileName);
        }
        
        /// <summary>
        /// Gets the config file path with support for custom config file
        /// Searches in order: application directory (for user editing), embedded resource (fallback)
        /// </summary>
        public string GetConfigFilePathWithFallback(string fileName)
        {
            // First check in the application directory (where exe is running)
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var appConfigPath = Path.Combine(baseDirectory, fileName);
            if (File.Exists(appConfigPath))
            {
                return appConfigPath;
            }
            
            // Check in parent directory (for bin folder scenarios)
            var parentDir = Directory.GetParent(baseDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parentDir))
            {
                var parentConfigPath = Path.Combine(parentDir, fileName);
                if (File.Exists(parentConfigPath))
                {
                    return parentConfigPath;
                }
            }
            
            // Check in project assets folder (for development)
            var assetsConfigPath = Path.Combine(_assetsFolder, fileName);
            if (File.Exists(assetsConfigPath))
            {
                return assetsConfigPath;
            }
            
            // Fall back to application directory (will create if needed)
            return appConfigPath;
        }
        
        /// <summary>
        /// Gets the path where users should edit the config file
        /// Returns the application directory location for user editing
        /// </summary>
        public string GetUserConfigPath(string fileName)
        {
            // Users should edit the config in the application directory (next to exe)
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, fileName);
        }
        
        /// <summary>
        /// Ensures default config exists, extracting from embedded resource if needed
        /// </summary>
        private string ExtractDefaultConfigIfNeeded()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var appConfigPath = Path.Combine(baseDirectory, _configFileName);
            
            // If config already exists in app directory, use that
            if (File.Exists(appConfigPath))
            {
                return appConfigPath;
            }
            
            // Try to extract from embedded resource
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "OCRTool.extraction_config.xml";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    
                    File.WriteAllText(appConfigPath, content);
                    System.Diagnostics.Debug.WriteLine($"[CONFIG] Created default config at: {appConfigPath}");
                    return appConfigPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Failed to extract embedded resource: {ex.Message}");
            }
            
            // Try assets folder as last resort
            var assetsConfigPath = Path.Combine(_assetsFolder, _configFileName);
            if (File.Exists(assetsConfigPath))
            {
                // Copy to application directory
                File.Copy(assetsConfigPath, appConfigPath, overwrite: true);
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Copied config from assets to: {appConfigPath}");
                return appConfigPath;
            }
            
            return appConfigPath;
        }

        /// <summary>
        /// Checks if the configuration file exists
        /// </summary>
        public bool ConfigFileExists()
        {
            var configPath = Path.Combine(_assetsFolder, _configFileName);
            return File.Exists(configPath);
        }

        /// <summary>
        /// Reloads configuration from file (for refreshing settings)
        /// </summary>
        public ExtractionConfig ReloadConfiguration()
        {
            _cachedConfig = null;
            return LoadConfiguration();
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        private void ValidateConfiguration(ExtractionConfig config)
        {
            if (config == null)
            {
                throw new ArgumentException("Configuration cannot be null");
            }

            if (config.Patterns == null)
            {
                config.Patterns = new List<PatternDefinition>();
            }

            if (config.EquipmentKeywords == null)
            {
                config.EquipmentKeywords = new List<string>();
            }

            if (config.RejectLines == null)
            {
                config.RejectLines = new List<string>();
            }

            // Ensure OCR settings exist
            if (config.OCR == null)
            {
                config.OCR = new OCRSettings();
            }
        }

        /// <summary>
        /// Gets the application version
        /// </summary>
        public string GetApplicationVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }

        /// <summary>
        /// Gets the application name
        /// </summary>
        public string GetApplicationName()
        {
            return "OCRTool - Tags & Equipment Extractor";
        }
    }
}
