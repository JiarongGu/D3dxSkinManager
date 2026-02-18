using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services
{
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
}
