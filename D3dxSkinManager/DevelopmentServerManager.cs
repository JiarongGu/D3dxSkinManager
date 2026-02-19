using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace D3dxSkinManager;

/// <summary>
/// Manages the React development server for local development
/// </summary>
public class DevelopmentServerManager : IDisposable
{
    private Process? _npmProcess;
    private readonly string _clientPath;
    private readonly string _serverUrl;
    private bool _disposed;

    public DevelopmentServerManager(string clientPath, string serverUrl = "http://localhost:3000")
    {
        _clientPath = clientPath;
        _serverUrl = serverUrl;
    }

    /// <summary>
    /// Starts the React development server using npm
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("[DevServer] Starting React development server...");

            // Check if server is already running
            if (await IsServerRunningAsync())
            {
                Console.WriteLine("[DevServer] Server already running at " + _serverUrl);
                return true;
            }

            // Verify client directory exists
            if (!Directory.Exists(_clientPath))
            {
                Console.WriteLine($"[DevServer] Error: Client directory not found: {_clientPath}");
                return false;
            }

            // Check if node_modules exists
            var nodeModulesPath = Path.Combine(_clientPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                Console.WriteLine("[DevServer] node_modules not found. Running npm install...");
                await RunNpmInstallAsync();
            }

            // Start npm start process
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npm start",
                WorkingDirectory = _clientPath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _npmProcess = new Process { StartInfo = startInfo };

            // Handle output for debugging
            _npmProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Console.WriteLine($"[DevServer] {args.Data}");
                }
            };

            _npmProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Console.WriteLine($"[DevServer] Error: {args.Data}");
                }
            };

            _npmProcess.Start();
            _npmProcess.BeginOutputReadLine();
            _npmProcess.BeginErrorReadLine();

            Console.WriteLine("[DevServer] Waiting for server to be ready...");

            // Wait for server to be ready (max 60 seconds)
            var timeout = TimeSpan.FromSeconds(60);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("[DevServer] Startup cancelled");
                    Stop();
                    return false;
                }

                if (await IsServerRunningAsync())
                {
                    Console.WriteLine("[DevServer] Server is ready at " + _serverUrl);
                    return true;
                }

                await Task.Delay(1000, cancellationToken);
            }

            Console.WriteLine("[DevServer] Timeout waiting for server to start");
            Stop();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevServer] Error starting server: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs npm install in the client directory
    /// </summary>
    private async Task RunNpmInstallAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npm install",
                WorkingDirectory = _clientPath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Console.WriteLine($"[npm install] {args.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine("[DevServer] npm install failed with exit code: " + process.ExitCode);
            }
            else
            {
                Console.WriteLine("[DevServer] npm install completed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevServer] Error running npm install: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the development server is running
    /// </summary>
    private async Task<bool> IsServerRunningAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await httpClient.GetAsync(_serverUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops the development server
    /// </summary>
    public void Stop()
    {
        if (_npmProcess != null && !_npmProcess.HasExited)
        {
            Console.WriteLine("[DevServer] Stopping React development server...");

            try
            {
                // Kill the process tree (including child processes)
                KillProcessTree(_npmProcess.Id);
                _npmProcess.WaitForExit(5000);

                if (!_npmProcess.HasExited)
                {
                    _npmProcess.Kill();
                }

                Console.WriteLine("[DevServer] Server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DevServer] Error stopping server: {ex.Message}");
            }
            finally
            {
                _npmProcess?.Dispose();
                _npmProcess = null;
            }
        }
    }

    /// <summary>
    /// Kills a process and all its children
    /// </summary>
    private void KillProcessTree(int processId)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/pid {processId} /t /f",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevServer] Error killing process tree: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
