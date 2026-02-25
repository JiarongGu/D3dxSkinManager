using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Setting.Services;

namespace D3dxSkinManager.Modules.Core.Models
{
    /// <summary>
    /// Interface for application environment configuration
    /// Provides access to base directory, development mode flag, and log level
    /// Use this interface for dependency injection to make testing easier
    /// </summary>
    public interface IAppEnvironment
    {
        /// <summary>
        /// Gets the base directory where the application is running
        /// </summary>
        string BaseDirectory { get; }

        /// <summary>
        /// Gets whether the application is running in development mode
        /// </summary>
        bool IsDevelopment { get; }

        /// <summary>
        /// Gets or sets the configured minimum log level
        /// This is the central log level configuration used by LogHelper
        /// Can be changed at runtime by services that manage log settings
        /// </summary>
        LogLevel MinimumLogLevel { get; set; }
    }

    /// <summary>
    /// Production implementation of IAppEnvironment
    /// Reads configuration from file system and environment variables
    /// </summary>
    public class AppEnvironment : IAppEnvironment
    {
        private AppEnvironment() { }

        /// <summary>
        /// Creates an AppEnvironment instance with proper configuration
        /// Reads log level from settings file synchronously during initialization
        /// </summary>
        public static AppEnvironment Create(string baseDirectory)
        {
            var isDevelopment = CheckIfDevelopment(baseDirectory);
            var environment = new AppEnvironment
            {
                BaseDirectory = baseDirectory,
                IsDevelopment = isDevelopment
            };

            environment.MinimumLogLevel = ReadLogLevel(environment);
            return environment;
        }

        public required string BaseDirectory { get; set; }

        /// <summary>
        /// Gets whether the application is running in development mode
        /// </summary>
        public bool IsDevelopment { get; set; }

        /// <summary>
        /// Gets or sets the configured minimum log level
        /// This is the central log level configuration used by LogHelper
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; }


        private static LogLevel ReadLogLevel(IAppEnvironment environment) 
        { 
            var globalPathService = new GlobalPathService(environment);
            var globalSettingService = new GlobalSettingService(globalPathService, environment);
            return globalSettingService.GetLogLevelAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Checks if the application is in development mode
        /// </summary>
        private static bool CheckIfDevelopment(string baseDirectory)
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
                   File.Exists(Path.Combine(baseDirectory, ".dev"));
        }
    }
}
