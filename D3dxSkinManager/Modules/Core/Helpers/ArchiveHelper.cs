using SharpSevenZip;
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
    ExtractionResult ExtractArchive(string archivePath, string targetDirectory);
    Task<string> CompressFolderAsync(string folderPath, string outputPath, ArchiveFormat format = ArchiveFormat.Zip, Action<int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<ArchiveValidationResult> ValidateArchiveAsync(string archivePath);
}

/// <summary>
/// Service for archive/compression operations using SharpSevenZip
/// Extraction: Fast 7z/LZMA extraction (10x+ faster than pure managed)
/// Compression: Supports ZIP, 7Z, TAR formats
/// Requires: Native 7z.dll library (placed in libs/ folder)
/// </summary>
public class ArchiveHelper : IArchiveHelper
{
    private readonly ILogHelper _logger;
    private static bool _sevenZipInitialized;
    private static readonly object _initLock = new();

    public ArchiveHelper(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialize 7z.dll library path for SharpSevenZip
    /// Thread-safe, idempotent initialization
    /// </summary>
    private void InitializeSevenZip()
    {
        if (_sevenZipInitialized)
            return;

        lock (_initLock)
        {
            if (_sevenZipInitialized)
                return;

            try
            {
                // Build 7z.dll path (architecture-specific DLL next to EXE)
                var architecture = Environment.Is64BitProcess ? "x64" : "x86";
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var sevenZipPath = Path.Combine(baseDirectory, "libs", "7z.dll");

                _logger.Info($"Initializing 7z.dll for {architecture} architecture", "ArchiveHelper");
                _logger.Verbose($"7z.dll path: {sevenZipPath}", "ArchiveHelper");

                // Verify 7z.dll exists
                if (!File.Exists(sevenZipPath))
                {
                    throw new FileNotFoundException(
                        $"7z.dll not found at: {sevenZipPath}. " +
                        $"Please download the official 7-Zip Extra package from https://7-zip.org/download.html " +
                        $"and copy the {architecture}/7z.dll to the libs/ folder. " +
                        $"See libs/README.md for detailed instructions.",
                        sevenZipPath);
                }

                // Set library path for SharpSevenZip
                SharpSevenZipBase.SetLibraryPath(sevenZipPath);

                _sevenZipInitialized = true;
                _logger.Info($"7z.dll initialized successfully from: {sevenZipPath}", "ArchiveHelper");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize 7z.dll: {ex.Message}", "ArchiveHelper", ex);
                throw new InvalidOperationException($"Failed to initialize 7-Zip library: {ex.Message}", ex);
            }
        }
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
                _logger.Warn($"Failed to detect archive type: {ex.Message}", "ArchiveHelper");
                return null;
            }
        });
    }

    /// <summary>
    /// Extract archive using SharpSevenZip with native 7z.dll (async version)
    /// Provides 10x+ faster extraction compared to pure managed implementations
    /// Supports: ZIP, 7Z, RAR, TAR, GZIP, BZIP2, XZ, ISO, and more
    /// </summary>
    public async Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        return await Task.Run(() => ExtractArchive(archivePath, targetDirectory)).ConfigureAwait(false);
    }

    /// <summary>
    /// Extract archive using SharpSevenZip with native 7z.dll (sync version)
    /// Provides 10x+ faster extraction compared to pure managed implementations
    /// Supports: ZIP, 7Z, RAR, TAR, GZIP, BZIP2, XZ, ISO, and more
    /// </summary>
    public ExtractionResult ExtractArchive(string archivePath, string targetDirectory)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        var result = new ExtractionResult();

        try
        {
            // Create target directory if it doesn't exist
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            _logger.Info($"Extracting {Path.GetFileName(archivePath)}...", "ArchiveHelper");

            // Detect archive type from magic bytes
            result.DetectedType = DetectArchiveTypeAsync(archivePath).GetAwaiter().GetResult();

            if (result.DetectedType != null)
            {
                _logger.Info($"Detected archive type: {result.DetectedType}", "ArchiveHelper");
            }

            // Initialize 7z.dll library (uses libs/7z.dll)
            InitializeSevenZip();

            // Use SharpSevenZip for extraction (supports all common formats)
            using var extractor = new SharpSevenZipExtractor(archivePath);
            extractor.ExtractArchive(targetDirectory);

            result.Success = true;
            result.FileCount = (int)extractor.FilesCount;
            _logger.Info($"Extracted {extractor.FilesCount} files from {Path.GetFileName(archivePath)}", "ArchiveHelper");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.Error($"Extraction failed: {ex.Message}", "ArchiveHelper", ex);
            throw new InvalidOperationException($"Archive extraction failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate an archive file to check if it's valid and detect password protection
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

        return await Task.Run(() =>
        {
            var result = new ArchiveValidationResult { IsValid = true };

            try
            {
                // Detect archive type from magic bytes
                result.DetectedType = DetectArchiveTypeAsync(archivePath).GetAwaiter().GetResult();

                if (result.DetectedType == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Not a recognized archive format (supported: ZIP, 7Z, RAR, TAR, GZIP, BZIP2)";
                    return result;
                }

                _logger.Info($"Validating archive type: {result.DetectedType}", "ArchiveHelper");

                // Initialize 7z.dll library
                InitializeSevenZip();

                // Try to open the archive to check password protection
                try
                {
                    using var extractor = new SharpSevenZipExtractor(archivePath);

                    // Check if we can read file list - if it throws, it's likely password protected
                    var fileCount = extractor.FilesCount;

                    // Try to get archive information
                    try
                    {
                        var archiveFileNames = extractor.ArchiveFileNames;
                        result.IsPasswordProtected = false;
                    }
                    catch
                    {
                        result.IsPasswordProtected = true;
                    }

                    result.IsValid = true;
                    _logger.Info($"Archive validation successful. Type: {result.DetectedType}, Password protected: {result.IsPasswordProtected}", "ArchiveHelper");
                }
                catch (Exception ex)
                {
                    // Check if the error is related to password protection
                    if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Wrong password", StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsValid = false;
                        result.IsPasswordProtected = true;
                        result.ErrorMessage = "Archive is password protected. Password-protected archives are not supported.";
                        _logger.Warn($"Archive is password protected: {archivePath}", "ArchiveHelper");
                    }
                    else
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Archive validation failed: {ex.Message}";
                        _logger.Error($"Archive validation error: {ex.Message}", "ArchiveHelper", ex);
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
    /// Compress a folder into an archive file using SharpSevenZip
    /// Supports: ZIP, 7Z, TAR formats
    /// </summary>
    public async Task<string> CompressFolderAsync(
        string folderPath,
        string outputPath,
        ArchiveFormat format = ArchiveFormat.Zip,
        Action<int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        var result = await Task.Run(() =>
        {
            _logger.Info($"Compressing folder: {Path.GetFileName(folderPath)} -> {Path.GetFileName(outputPath)}", "ArchiveHelper");

            // Initialize 7z.dll library
            InitializeSevenZip();

            // Create output directory if needed
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Create compressor with specified format
            var compressor = new SharpSevenZipCompressor
            {
                ArchiveFormat = format switch
                {
                    ArchiveFormat.SevenZip => OutArchiveFormat.SevenZip,
                    ArchiveFormat.Tar => OutArchiveFormat.Tar,
                    ArchiveFormat.Zip => OutArchiveFormat.Zip,
                    _ => OutArchiveFormat.Zip
                },
                // Use Fast compression to reduce CPU usage and improve responsiveness
                CompressionLevel = CompressionLevel.Fast,
                PreserveDirectoryRoot = false  // Don't include root folder name in archive
            };

            // Wire up progress reporting if callback provided
            if (progressCallback != null)
            {
                compressor.Compressing += (sender, e) =>
                {
                    progressCallback((int)e.PercentDone);
                };
            }

            try
            {
                // Compress the directory
                compressor.CompressDirectory(folderPath, outputPath);

                var fileInfo = new FileInfo(outputPath);
                _logger.Info($"Compressed to {Path.GetFileName(outputPath)} ({fileInfo.Length / 1024} KB)", "ArchiveHelper");

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.Error($"Folder compression failed: {ex.Message}", "ArchiveHelper", ex);
                throw new InvalidOperationException($"Failed to compress folder: {ex.Message}", ex);
            }
        });

        // Check for cancellation AFTER compression completes
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.Info("Compression was cancelled", "ArchiveHelper");
            return await Task.FromCanceled<string>(cancellationToken);
        }

        return result;
    }
}
