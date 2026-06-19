namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Progress of an in-flight download. Reported repeatedly via <see cref="System.IProgress{T}"/>.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>Bytes written to disk so far.</summary>
    public long BytesReceived { get; init; }

    /// <summary>Total bytes if the server sent Content-Length; null when unknown.</summary>
    public long? TotalBytes { get; init; }

    /// <summary>0–100 when the total is known; null for indeterminate progress.</summary>
    public int? Percent { get; init; }
}

/// <summary>A request to download a URL to a file.</summary>
public sealed class DownloadRequest
{
    /// <summary>The http/https URL to download.</summary>
    public required string Url { get; init; }

    /// <summary>Absolute destination file path (parent dirs are created).</summary>
    public required string DestinationPath { get; init; }

    /// <summary>Optional expected sha256 (hex, lowercase). When set, a mismatch deletes the file and throws.</summary>
    public string? ExpectedSha256 { get; init; }

    /// <summary>Optional extra request headers (e.g. Accept).</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>The outcome of a successful download.</summary>
public sealed class DownloadResult
{
    public required string FilePath { get; init; }
    public long Bytes { get; init; }
    /// <summary>sha256 of the downloaded bytes (hex, lowercase) — computed during the stream.</summary>
    public required string Sha256 { get; init; }
}

/// <summary>A file in the managed downloads directory.</summary>
public sealed class ManagedDownloadInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public long Size { get; init; }
    public DateTime ModifiedUtc { get; init; }
}

/// <summary>Result of cleaning the managed downloads directory.</summary>
public sealed class DownloadCleanupResult
{
    public int DeletedCount { get; init; }
    public long BytesFreed { get; init; }
}
