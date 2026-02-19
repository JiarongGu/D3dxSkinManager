using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Service for launching external processes
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Launch an external process with arguments
    /// </summary>
    /// <param name="executablePath">Path to the executable</param>
    /// <param name="arguments">Command line arguments</param>
    /// <param name="workingDirectory">Optional working directory</param>
    Task LaunchProcessAsync(string executablePath, string? arguments = null, string? workingDirectory = null);
}

/// <summary>
/// Implementation of process launching service
/// </summary>
public class ProcessService : IProcessService
{
    private readonly IPathValidator _pathValidator;

    public ProcessService(IPathValidator pathValidator)
    {
        _pathValidator = pathValidator;
    }

    /// <summary>
    /// Launch an external process with arguments
    /// </summary>
    public async Task LaunchProcessAsync(string executablePath, string? arguments = null, string? workingDirectory = null)
    {
        _pathValidator.ValidatePathNotEmpty(executablePath, nameof(executablePath));
        _pathValidator.ValidateFileExists(executablePath);

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
