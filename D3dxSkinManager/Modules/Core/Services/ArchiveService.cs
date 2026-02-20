using System;
using System.IO;
using System.Threading.Tasks;
using SevenZip;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Result of archive extraction operation
/// </summary>
public class ExtractionResult
{
    public bool Success { get; set; }
    public string? DetectedType { get; set; }
    public int FileCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for archive operations
/// </summary>
public interface IArchiveService
{
    Task<string?> DetectArchiveTypeAsync(string archivePath);
    Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory);
}

/// <summary>
/// Service for archive/compression operations
/// Responsibility: Archive format detection and extraction with multiple fallback strategies
/// Supports: ZIP, 7Z, RAR, TAR, GZIP, BZIP2 with or without file extensions
/// </summary>
public class ArchiveService : IArchiveService
{
    private readonly ILogHelper _logger;

    public ArchiveService(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detect archive type from file magic bytes (header signature)
    /// Returns extension without dot (e.g., "zip", "7z", "rar")
    /// Supports: ZIP, 7Z, RAR (v4 & v5), TAR, GZIP, BZIP2
    /// </summary>
    public async Task<string?> DetectArchiveTypeAsync(string archivePath)
    {
        if (!File.Exists(archivePath))
            return null;

        return await Task.Run(() =>
        {
            try
            {
                using var fileStream = File.OpenRead(archivePath);

                // Read first 8 bytes for magic number detection
                var buffer = new byte[8];
                var bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                if (bytesRead < 2)
                    return null;

                // ZIP: PK (50 4B)
                if (buffer[0] == 0x50 && buffer[1] == 0x4B)
                    return "zip";

                // 7Z: 7z (37 7A BC AF 27 1C)
                if (bytesRead >= 6 &&
                    buffer[0] == 0x37 && buffer[1] == 0x7A &&
                    buffer[2] == 0xBC && buffer[3] == 0xAF &&
                    buffer[4] == 0x27 && buffer[5] == 0x1C)
                    return "7z";

                // RAR v5: Rar! (52 61 72 21 1A 07 01 00)
                if (bytesRead >= 8 &&
                    buffer[0] == 0x52 && buffer[1] == 0x61 &&
                    buffer[2] == 0x72 && buffer[3] == 0x21 &&
                    buffer[4] == 0x1A && buffer[5] == 0x07 &&
                    buffer[6] == 0x01 && buffer[7] == 0x00)
                    return "rar";

                // RAR v4: Rar! (52 61 72 21 1A 07 00)
                if (bytesRead >= 7 &&
                    buffer[0] == 0x52 && buffer[1] == 0x61 &&
                    buffer[2] == 0x72 && buffer[3] == 0x21 &&
                    buffer[4] == 0x1A && buffer[5] == 0x07 &&
                    buffer[6] == 0x00)
                    return "rar";

                // GZIP: (1F 8B)
                if (buffer[0] == 0x1F && buffer[1] == 0x8B)
                    return "gz";

                // BZIP2: BZ (42 5A 68)
                if (bytesRead >= 3 &&
                    buffer[0] == 0x42 && buffer[1] == 0x5A && buffer[2] == 0x68)
                    return "bz2";

                // TAR: Check for "ustar" at offset 257 (TAR header signature)
                if (fileStream.Length > 262)
                {
                    fileStream.Seek(257, SeekOrigin.Begin);
                    var tarBuffer = new byte[5];
                    if (fileStream.Read(tarBuffer, 0, 5) == 5)
                    {
                        var ustar = System.Text.Encoding.ASCII.GetString(tarBuffer);
                        if (ustar == "ustar")
                            return "tar";
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to detect archive type: {ex.Message}", "ArchiveService");
                return null;
            }
        });
    }

    /// <summary>
    /// Extract archive using SevenZipSharp which supports most formats:
    /// ZIP, 7Z, RAR, TAR, GZIP, BZIP2, XZ, ISO, and more
    /// Returns ExtractionResult with success status and detected type
    /// </summary>
    public async Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        return await Task.Run(() =>
        {
            var result = new ExtractionResult();

            try
            {
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                _logger.Info($"Extracting {Path.GetFileName(archivePath)}...", "ArchiveService");

                // Detect archive type from magic bytes
                result.DetectedType = DetectArchiveTypeAsync(archivePath).GetAwaiter().GetResult();

                if (result.DetectedType != null)
                {
                    _logger.Info($"Detected archive type: {result.DetectedType}", "ArchiveService");
                }

                // Set library path for 7z.dll (required by SevenZipSharp)
                // The 7z.Libs package places DLLs in x64/x86 subdirectories
                var platformFolder = Environment.Is64BitProcess ? "x64" : "x86";
                var libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, platformFolder, "7z.dll");

                if (File.Exists(libraryPath))
                {
                    SevenZipBase.SetLibraryPath(libraryPath);
                }
                else
                {
                    _logger.Warning($"7z.dll not found at: {libraryPath}", "ArchiveService");
                }

                // Use SevenZipSharp for extraction (supports all common formats)
                using var extractor = new SevenZipExtractor(archivePath);
                extractor.ExtractArchive(targetDirectory);

                result.Success = true;
                result.FileCount = (int)extractor.FilesCount;
                _logger.Info($"Extracted {extractor.FilesCount} files from {Path.GetFileName(archivePath)}", "ArchiveService");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.Error($"Extraction failed: {ex.Message}", "ArchiveService", ex);
                throw new InvalidOperationException($"Archive extraction failed: {ex.Message}", ex);
            }
        });
    }

}
