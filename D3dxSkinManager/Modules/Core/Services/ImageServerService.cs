using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace D3dxSkinManager.Modules.Core.Services;

public interface IImageServerService: IDisposable
{
    /// <summary>
    /// Convert an image file path to a local HTTP URL served by this service
    /// If the path is already an HTTP URL, it will be returned as-is
    /// </summary>
    string? ConvertPathToUrl(string? path);

    /// <summary>
    /// Start the image server
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop the image server
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Simple HTTP server for serving image files
/// Runs on localhost to bypass file:// protocol restrictions
/// </summary>
public class ImageServerService : IImageServerService, IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _dataPath;
    private readonly int _port;
    private readonly ILogHelper _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;

    public ImageServerService(string dataPath, ILogHelper logger, int port = 5555)
    {
        _dataPath = dataPath;
        _port = port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
    }

    public string GetImageUrl(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        // Convert absolute path to relative path from data directory
        var relativePath = Path.GetRelativePath(_dataPath, filePath);
        // URL encode and replace backslashes with forward slashes
        var urlPath = relativePath.Replace('\\', '/');
        return $"http://localhost:{_port}/images/{urlPath}";
    }

    /// <summary>
    /// Convert image path or paths in an object to HTTP URLs
    /// Centralized method to avoid duplication
    /// </summary>
    public string? ConvertPathToUrl(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Path is already an HTTP URL, return as-is
        if (path.StartsWith("http://") || path.StartsWith("https://"))
            return path;

        // Convert to HTTP URL
        return GetImageUrl(path);
    }

    public async Task StartAsync()
    {
        try
        {
            _listener.Start();
            _cancellationTokenSource = new CancellationTokenSource();
            _serverTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
            _logger.Info($"Started on http://localhost:{_port}", "ImageServer");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start: {ex.Message}", "ImageServer", ex);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            if (_serverTask != null)
            {
                await _serverTask;
            }
        }

        _listener.Stop();
        _logger.Info("Stopped", "ImageServer");
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error accepting request: {ex.Message}", "ImageServer", ex);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Only handle /images/* requests
            if (!request.Url?.AbsolutePath.StartsWith("/images/") ?? true)
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            // Extract file path from URL
            var urlPath = request.Url?.AbsolutePath.Substring("/images/".Length) ?? "";
            urlPath = HttpUtility.UrlDecode(urlPath);
            var filePath = Path.Combine(_dataPath, urlPath.Replace('/', '\\'));

            // Security check: ensure file is within data directory
            var fullPath = Path.GetFullPath(filePath);
            var dataPathFull = Path.GetFullPath(_dataPath);
            if (!fullPath.StartsWith(dataPathFull, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning($"Security violation: Attempted to access {fullPath}", "ImageServer");
                response.StatusCode = 403;
                response.Close();
                return;
            }

            // Check if file exists
            if (!File.Exists(fullPath))
            {
                _logger.Warning($"File not found: {fullPath}", "ImageServer");
                response.StatusCode = 404;
                response.Close();
                return;
            }

            // Determine content type
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            response.ContentType = extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".avif" => "image/avif",
                ".tif" or ".tiff" => "image/tiff",
                _ => "application/octet-stream"
            };

            // Enable CORS for local access
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Cache-Control", "public, max-age=86400"); // Cache for 1 day

            // Read and send file
            var fileBytes = await File.ReadAllBytesAsync(fullPath);
            response.ContentLength64 = fileBytes.Length;
            response.StatusCode = 200;

            await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
            response.Close();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling request: {ex.Message}", "ImageServer", ex);
            response.StatusCode = 500;
            response.Close();
        }
    }

    public void Dispose()
    {
        StopAsync().Wait();
        _listener.Close();
        _cancellationTokenSource?.Dispose();
    }
}
