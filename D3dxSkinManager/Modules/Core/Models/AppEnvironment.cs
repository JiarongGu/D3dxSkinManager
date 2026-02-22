using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Settings.Services;

namespace D3dxSkinManager.Modules.Core.Models
{
    public class AppEnvironment
    {
        private AppEnvironment() { }

        /// <summary>
        /// Creates an AppEnvironment instance with proper configuration
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


        private static LogLevel ReadLogLevel(AppEnvironment environment) 
        { 
            var globalPathService = new GlobalPathService(environment);
            var globalSettingService = new GlobalSettingsService(globalPathService, environment);
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
