using System.Reflection;
using System.Text;

namespace D3dxSkinManager.Infrastructure.Resources;

/// <summary>
/// Provides access to embedded resources with automatic fallback to file system in development
/// </summary>
public class EmbeddedResourceProvider : IEmbeddedResourceProvider
{
    private readonly string _baseDirectory;
    private readonly Assembly _assembly;
    private readonly Dictionary<string, string> _resourceManifest;
    private readonly bool _isEmbeddedMode;

    public bool IsEmbeddedMode => _isEmbeddedMode;

    public EmbeddedResourceProvider()
    {
        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _assembly = Assembly.GetExecutingAssembly();
        _resourceManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Determine if we're in embedded mode (production) or file-based mode (development)
        _isEmbeddedMode = DetermineEmbeddedMode();

        if (_isEmbeddedMode)
        {
            BuildResourceManifest();
            Console.WriteLine($"[EmbeddedResourceProvider] Running in EMBEDDED mode ({_resourceManifest.Count} resources available)");
        }
        else
        {
            Console.WriteLine("[EmbeddedResourceProvider] Running in FILE-BASED mode (development)");
        }
    }

    public Stream? GetResourceStream(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath))
            return null;

        // Normalize path separators
        virtualPath = NormalizePath(virtualPath);

        if (_isEmbeddedMode)
        {
            return GetEmbeddedStream(virtualPath);
        }
        else
        {
            return GetFileStream(virtualPath);
        }
    }

    public string? GetResourceString(string virtualPath)
    {
        using var stream = GetResourceStream(virtualPath);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public bool ResourceExists(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath))
            return false;

        virtualPath = NormalizePath(virtualPath);

        if (_isEmbeddedMode)
        {
            return _resourceManifest.ContainsKey(virtualPath);
        }
        else
        {
            var filePath = Path.Combine(_baseDirectory, virtualPath);
            return File.Exists(filePath);
        }
    }

    public IEnumerable<string> GetAllResourcePaths()
    {
        if (_isEmbeddedMode)
        {
            return _resourceManifest.Keys;
        }
        else
        {
            // In file-based mode, enumerate wwwroot and data directories
            var paths = new List<string>();

            var wwwrootPath = Path.Combine(_baseDirectory, "wwwroot");
            if (Directory.Exists(wwwrootPath))
            {
                var wwwrootFiles = Directory.GetFiles(wwwrootPath, "*", SearchOption.AllDirectories);
                paths.AddRange(wwwrootFiles.Select(f => f.Substring(_baseDirectory.Length).TrimStart('\\', '/')));
            }

            var dataPath = Path.Combine(_baseDirectory, "data");
            if (Directory.Exists(dataPath))
            {
                var dataFiles = Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories);
                paths.AddRange(dataFiles.Select(f => f.Substring(_baseDirectory.Length).TrimStart('\\', '/')));
            }

            return paths;
        }
    }

    private bool DetermineEmbeddedMode()
    {
        var wwwrootPath = Path.Combine(_baseDirectory, "wwwroot");
        var hasFileBasedResources = Directory.Exists(wwwrootPath);

        // Check if we have embedded resources in the assembly
        var embeddedNames = _assembly.GetManifestResourceNames();
        var hasEmbeddedResources = embeddedNames.Any(name =>
            name.StartsWith("D3dxSkinManager.wwwroot.", StringComparison.OrdinalIgnoreCase));

        // Also check for development mode indicators
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
                           File.Exists(Path.Combine(_baseDirectory, ".dev"));

        Console.WriteLine($"[EmbeddedResourceProvider] Detection:");
        Console.WriteLine($"  - Base directory: {_baseDirectory}");
        Console.WriteLine($"  - wwwroot path: {wwwrootPath}");
        Console.WriteLine($"  - Has file-based wwwroot: {hasFileBasedResources}");
        Console.WriteLine($"  - Has embedded resources: {hasEmbeddedResources}");
        Console.WriteLine($"  - Total embedded resources: {embeddedNames.Length}");
        Console.WriteLine($"  - Is development mode: {isDevelopment}");

        // Use embedded mode if we have embedded resources and no file-based resources
        // OR if we have embedded resources and not in development mode
        bool useEmbeddedMode = hasEmbeddedResources && (!hasFileBasedResources || !isDevelopment);

        Console.WriteLine($"  → Using mode: {(useEmbeddedMode ? "EMBEDDED" : "FILE-BASED")}");

        return useEmbeddedMode;
    }

    private void BuildResourceManifest()
    {
        // Get all embedded resource names
        var resourceNames = _assembly.GetManifestResourceNames();

        Console.WriteLine($"[EmbeddedResourceProvider] Found {resourceNames.Length} total embedded resources");

        foreach (var resourceName in resourceNames)
        {
            // Only process resources that match our prefixes
            if (!resourceName.StartsWith("D3dxSkinManager.wwwroot.") &&
                !resourceName.StartsWith("D3dxSkinManager.data."))
                continue;

            // Convert resource name to virtual path
            // Example: D3dxSkinManager.wwwroot.assets.index-abc123.js -> wwwroot/assets/index-abc123.js
            var virtualPath = ConvertResourceNameToVirtualPath(resourceName);

            if (!string.IsNullOrEmpty(virtualPath))
            {
                _resourceManifest[virtualPath] = resourceName;
                Console.WriteLine($"[EmbeddedResourceProvider]   Mapped: {virtualPath} <- {resourceName}");
            }
        }

        if (_resourceManifest.Count == 0)
        {
            Console.WriteLine($"[EmbeddedResourceProvider] ⚠️ WARNING: No wwwroot resources found!");
            Console.WriteLine($"[EmbeddedResourceProvider] All resource names:");
            foreach (var name in resourceNames.Take(20))
            {
                Console.WriteLine($"[EmbeddedResourceProvider]   - {name}");
            }
        }
    }

    private string ConvertResourceNameToVirtualPath(string resourceName)
    {
        // Remove assembly prefix
        var prefix = "D3dxSkinManager.";
        if (!resourceName.StartsWith(prefix))
            return string.Empty;

        var path = resourceName.Substring(prefix.Length);

        // Replace dots with slashes, but preserve dots in file extensions
        // Strategy: Work backwards from the end to find the file extension
        var lastDotIndex = path.LastIndexOf('.');
        if (lastDotIndex == -1)
            return path; // No extension, just return as-is

        var extension = path.Substring(lastDotIndex); // e.g., ".js"
        var pathWithoutExtension = path.Substring(0, lastDotIndex); // e.g., "wwwroot.assets.index-abc123"

        // Replace remaining dots with slashes
        pathWithoutExtension = pathWithoutExtension.Replace('.', '/');

        // Normalize all backslashes to forward slashes (happens when %(RecursiveDir) contains backslashes on Windows)
        pathWithoutExtension = pathWithoutExtension.Replace('\\', '/');

        // Combine back and normalize the result
        var result = pathWithoutExtension + extension;
        return result.Replace('\\', '/'); // Ensure extension part is also normalized
    }

    private Stream? GetEmbeddedStream(string virtualPath)
    {
        if (!_resourceManifest.TryGetValue(virtualPath, out var resourceName))
        {
            Console.WriteLine($"[EmbeddedResourceProvider] Resource not found: {virtualPath}");
            return null;
        }

        try
        {
            var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Console.WriteLine($"[EmbeddedResourceProvider] Failed to load embedded resource: {resourceName}");
                return null;
            }

            // Copy to memory stream to allow seeking and multiple reads
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            stream.Dispose();

            return memoryStream;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmbeddedResourceProvider] Error loading embedded resource {resourceName}: {ex.Message}");
            return null;
        }
    }

    private Stream? GetFileStream(string virtualPath)
    {
        var filePath = Path.Combine(_baseDirectory, virtualPath);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            // Read file into memory stream to avoid file locking issues
            var fileBytes = File.ReadAllBytes(filePath);
            return new MemoryStream(fileBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmbeddedResourceProvider] Error reading file {filePath}: {ex.Message}");
            return null;
        }
    }

    private static string NormalizePath(string path)
    {
        // Normalize path separators to forward slashes
        path = path.Replace('\\', '/');

        // Remove leading slashes
        path = path.TrimStart('/');

        return path;
    }
}
