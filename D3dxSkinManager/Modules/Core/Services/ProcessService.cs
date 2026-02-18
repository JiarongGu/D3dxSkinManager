using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services
{
    /// <summary>
    /// Implementation of process launching service
    /// </summary>
    public class ProcessService : IProcessService
    {
        /// <summary>
        /// Launch an external process with arguments
        /// </summary>
        public async Task LaunchProcessAsync(string executablePath, string? arguments = null, string? workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));

            if (!File.Exists(executablePath))
                throw new FileNotFoundException($"Executable not found: {executablePath}", executablePath);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true,
                    WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? string.Empty
                };

                Process.Start(startInfo);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch process: {executablePath}", ex);
            }
        }
    }
}
