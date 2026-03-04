using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using Encoding = System.Text.Encoding;

namespace D3dxSkinManager.Modules.Core.Helpers;

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
/// Archive format for compression
/// NOTE: SharpCompress only supports WRITING to Zip and Tar formats.
/// SevenZip is kept for future compatibility but will throw NotSupportedException if used for compression.
/// Reading supports all formats (Zip, 7z, Tar, Rar, etc.)
/// </summary>
public enum ArchiveFormat
{
    Zip,
    SevenZip,
    Tar
}

/// <summary>
/// Result of archive validation operation
/// </summary>
public class ArchiveValidationResult
{
    public bool IsValid { get; set; }
    public string? DetectedType { get; set; }
    public bool IsPasswordProtected { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for archive operations
/// </summary>
public interface IArchiveHelper
{
    Task<string?> DetectArchiveTypeAsync(string archivePath);
    Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory);
    Task<string> CompressFolderAsync(string folderPath, string outputPath, ArchiveFormat format = ArchiveFormat.Zip, Action<int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<ArchiveValidationResult> ValidateArchiveAsync(string archivePath);
}

/// <summary>
/// Service for archive/compression operations using SharpCompress
/// Responsibility: Archive format detection and extraction with multiple fallback strategies
/// Supports: ZIP, 7Z, RAR, TAR, GZIP, BZIP2 - Pure managed, no native DLL dependencies
/// </summary>
public class ArchiveHelper : IArchiveHelper
{
    private readonly ILogHelper _logger;

    public ArchiveHelper(ILogHelper logger)
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
                        var ustar = Encoding.ASCII.GetString(tarBuffer);
                        if (ustar == "ustar")
                            return "tar";
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to detect archive type: {ex.Message}", "ArchiveService");
                return null;
            }
        });
    }

    /// <summary>
    /// Extract archive using SharpCompress which supports most formats:
    /// ZIP, 7Z, RAR, TAR, GZIP, BZIP2, XZ, ISO, and more
    /// Returns ExtractionResult with success status and detected type
    /// Pure managed code - no native DLL dependencies
    /// </summary>
    public async Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        return await Task.Run(async () =>
        {
            var result = new ExtractionResult();

            try
            {
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                _logger.Info($"Extracting {Path.GetFileName(archivePath)}...", "ArchiveService");

                // Detect archive type from magic bytes
                result.DetectedType = await DetectArchiveTypeAsync(archivePath).ConfigureAwait(false);

                if (result.DetectedType != null)
                {
                    _logger.Info($"Detected archive type: {result.DetectedType}", "ArchiveService");
                }

                // Use SharpCompress for extraction (pure managed, supports all common formats)
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                var fileCount = 0;

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    // SharpCompress 0.46.4: Use simple overload (defaults to ExtractFullPath=true, Overwrite=true)
                    entry.WriteToDirectory(targetDirectory);
                    fileCount++;
                }

                result.Success = true;
                result.FileCount = fileCount;
                _logger.Info($"Extracted {fileCount} files from {Path.GetFileName(archivePath)}", "ArchiveService");
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

    /// <summary>
    /// Validate an archive file to check if it's a valid compressed file and detect password protection
    /// Returns validation result with detected type and password protection status
    /// </summary>
    public async Task<ArchiveValidationResult> ValidateArchiveAsync(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            return new ArchiveValidationResult
            {
                IsValid = false,
                ErrorMessage = "Archive file not found"
            };
        }

        return await Task.Run(async () =>
        {
            var result = new ArchiveValidationResult { IsValid = true };

            try
            {
                // Detect archive type from magic bytes
                result.DetectedType = await DetectArchiveTypeAsync(archivePath).ConfigureAwait(false);

                if (result.DetectedType == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Not a recognized archive format (supported: ZIP, 7Z, RAR, TAR, GZIP, BZIP2)";
                    return result;
                }

                _logger.Info($"Validating archive type: {result.DetectedType}", "ArchiveService");

                // Try to open the archive to check password protection and validity
                try
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath);

                    // Check if archive entries are encrypted
                    var hasEncryptedEntries = archive.Entries.Any(e => e.IsEncrypted);

                    if (hasEncryptedEntries)
                    {
                        result.IsValid = false;
                        result.IsPasswordProtected = true;
                        result.ErrorMessage = "Archive is password protected. Password-protected archives are not supported.";
                        _logger.Warn($"Archive is password protected: {archivePath}", "ArchiveService");
                    }
                    else
                    {
                        result.IsValid = true;
                        result.IsPasswordProtected = false;
                        _logger.Info($"Archive validation successful. Type: {result.DetectedType}, Password protected: {result.IsPasswordProtected}", "ArchiveService");
                    }
                }
                catch (Exception ex)
                {
                    // Check if the error is related to password protection or encryption
                    if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Wrong password", StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsValid = false;
                        result.IsPasswordProtected = true;
                        result.ErrorMessage = "Archive is password protected. Password-protected archives are not supported.";
                        _logger.Warn($"Archive is password protected: {archivePath}", "ArchiveService");
                    }
                    else
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Archive validation failed: {ex.Message}";
                        _logger.Error($"Archive validation error: {ex.Message}", "ArchiveService", ex);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ArchiveValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Validation error: {ex.Message}"
                };
            }
        });
    }

    /// <summary>
    /// Compress a folder into an archive file
    /// Supports: ZIP, 7Z, TAR formats
    /// Pure managed code - no native DLL dependencies
    /// </summary>
    /// <param name="folderPath">Path to folder to compress</param>
    /// <param name="outputPath">Output archive file path</param>
    /// <param name="format">Archive format (default: ZIP)</param>
    /// <param name="progressCallback">Optional callback for progress updates (0-100)</param>
    /// <returns>Path to created archive</returns>
    public async Task<string> CompressFolderAsync(
        string folderPath,
        string outputPath,
        ArchiveFormat format = ArchiveFormat.Zip,
        Action<int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        // Validate format - SharpCompress only supports writing Zip and Tar
        if (format == ArchiveFormat.SevenZip)
        {
            _logger.Warn("SevenZip format requested but SharpCompress does not support writing 7z archives. Falling back to Zip format.", "ArchiveService");
            format = ArchiveFormat.Zip;
        }

        var result = await Task.Run(() =>
        {
            _logger.Info($"Compressing folder: {Path.GetFileName(folderPath)} -> {Path.GetFileName(outputPath)} (format: {format})", "ArchiveService");

            // Create output directory if needed
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            try
            {
                // Determine SharpCompress archive type and writer options
                var archiveType = format switch
                {
                    ArchiveFormat.SevenZip => ArchiveType.SevenZip,
                    ArchiveFormat.Tar => ArchiveType.Tar,
                    ArchiveFormat.Zip => ArchiveType.Zip,
                    _ => ArchiveType.Zip
                };

                var compressionType = format switch
                {
                    ArchiveFormat.SevenZip => CompressionType.LZMA,
                    ArchiveFormat.Tar => CompressionType.GZip,
                    ArchiveFormat.Zip => CompressionType.Deflate,
                    _ => CompressionType.Deflate
                };

                var writerOptions = new WriterOptions(compressionType)
                {
                    LeaveStreamOpen = false,
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Default = Encoding.UTF8
                    }
                };

                // Get all files in the folder recursively
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                var totalFiles = files.Length;
                var processedFiles = 0;

                using var stream = File.Create(outputPath);
                using var writer = WriterFactory.OpenWriter(stream, archiveType, writerOptions);

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Info("Compression was cancelled", "ArchiveService");
                        throw new OperationCanceledException(cancellationToken);
                    }

                    // Get relative path for the archive entry
                    var relativePath = Path.GetRelativePath(folderPath, file);

                    // Add file to archive
                    writer.Write(relativePath, file);

                    processedFiles++;

                    // Report progress
                    if (progressCallback != null && totalFiles > 0)
                    {
                        var progress = (int)((double)processedFiles / totalFiles * 100);
                        progressCallback(progress);
                    }
                }

                var fileInfo = new FileInfo(outputPath);
                _logger.Info($"Compressed to {Path.GetFileName(outputPath)} ({fileInfo.Length / 1024} KB)", "ArchiveService");

                return outputPath;
            }
            catch (OperationCanceledException)
            {
                // Clean up partial file if cancelled
                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { /* Ignore cleanup errors */ }
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Folder compression failed: {ex.Message}", "ArchiveService", ex);
                throw new InvalidOperationException($"Failed to compress folder: {ex.Message}", ex);
            }
        }, cancellationToken);

        return result;
    }
}
