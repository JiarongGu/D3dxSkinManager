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
    Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory, string? password = null);
    ExtractionResult ExtractArchive(string archivePath, string targetDirectory, string? password = null);

    /// <summary>Extract + recursively unwrap nested archives (magic-byte detection, so a disguised
    /// extension still unwraps) until only real content remains. Returns the layer count unwrapped.</summary>
    Task<int> ExtractArchiveRecursiveAsync(string archivePath, string targetDirectory, string? password = null, int maxDepth = 8);

    /// <summary>Cheap magic-byte test: is this file an extractable archive regardless of extension?</summary>
    bool IsArchiveFile(string path);
    Task<string> CompressFolderAsync(string folderPath, string outputPath, ArchiveFormat format = ArchiveFormat.SevenZip, CompressionLevel compressionLevel = CompressionLevel.High, Action<int>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<ArchiveValidationResult> ValidateArchiveAsync(string archivePath);

    /// <summary>
    /// Update (replace/add) a SINGLE file inside an existing archive without recompressing the whole
    /// archive. <paramref name="entryPath"/> is the path inside the archive (relative, e.g. "sub/mod.ini").
    /// Much faster than re-compressing the full folder for a small edit.
    /// </summary>
    Task UpdateFileInArchiveAsync(string archivePath, string sourceFilePath, string entryPath);
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
    public async Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string targetDirectory, string? password = null)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        return await Task.Run(() => ExtractArchive(archivePath, targetDirectory, password)).ConfigureAwait(false);
    }

    /// <summary>
    /// Extract archive using SharpSevenZip with native 7z.dll (sync version)
    /// Provides 10x+ faster extraction compared to pure managed implementations
    /// Supports: ZIP, 7Z, RAR, TAR, GZIP, BZIP2, XZ, ISO, and more.
    /// A password is safely ignored by unencrypted archives — pass it whenever one MIGHT apply.
    /// </summary>
    public ExtractionResult ExtractArchive(string archivePath, string targetDirectory, string? password = null)
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

            // Extract with an explicit format, not the file extension. Sites disguise mods with a fake
            // extension (huihui ships them as "1.mp4", a 网盘 "safe keep" trick) — and the disguise is
            // often a POLYGLOT (a real media header with a zip APPENDED at the end), so first-bytes
            // magic detection also misses it. So: try the magic-byte format first, then fall back to
            // trying the common archive formats explicitly (a zip's directory is read from the END,
            // so a media+zip polyglot extracts fine as Zip). Only a genuine non-archive fails them all.
            var candidates = new List<InArchiveFormat>();
            var detected = ToInArchiveFormat(result.DetectedType);
            if (detected.HasValue) candidates.Add(detected.Value);
            foreach (var f in new[] { InArchiveFormat.Zip, InArchiveFormat.SevenZip, InArchiveFormat.Rar, InArchiveFormat.Tar, InArchiveFormat.GZip, InArchiveFormat.BZip2 })
                if (!candidates.Contains(f)) candidates.Add(f);

            Exception? lastError = null;
            Exception? passwordError = null;
            foreach (var fmt in candidates)
            {
                try
                {
                    if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, recursive: true);
                    Directory.CreateDirectory(targetDirectory);
                    using var extractor = string.IsNullOrEmpty(password)
                        ? new SharpSevenZipExtractor(archivePath, fmt)
                        : new SharpSevenZipExtractor(archivePath, password, fmt);
                    extractor.ExtractArchive(targetDirectory);
                    result.Success = true;
                    result.FileCount = (int)extractor.FilesCount;
                    result.DetectedType ??= fmt.ToString().ToLowerInvariant();
                    _logger.Info($"Extracted {extractor.FilesCount} files from {Path.GetFileName(archivePath)} (format {fmt})", "ArchiveHelper");
                    return result;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    // Remember a password-suspect failure but DON'T bail yet — a polyglot's wrong-offset
                    // read also looks like a "data error", and the carve fallback below is what fixes it.
                    if (IsPasswordError(ex)) passwordError ??= ex;
                }
            }

            // No format opened the WHOLE file — it may be a POLYGLOT (huihui's "safe keep": a real
            // media file with an archive APPENDED, whose internal offsets are relative to the archive
            // start, so a whole-file reader rejects it). Find the embedded archive signature and
            // extract from THAT offset (a carved-out standalone archive).
            var carved = TryCarveEmbeddedArchive(archivePath);
            if (carved != null)
            {
                try
                {
                    _logger.Info($"Polyglot detected in {Path.GetFileName(archivePath)} — extracting carved archive", "ArchiveHelper");
                    var inner = ExtractArchive(carved, targetDirectory, password);
                    inner.DetectedType ??= "carved";
                    return inner;
                }
                finally { try { File.Delete(carved); } catch { } }
            }
            // Prefer a password-suspect error (so the caller retries WITH a password) over a generic
            // wrong-format error from a later candidate.
            throw passwordError ?? lastError ?? new InvalidOperationException("no archive format matched");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.Error($"Extraction failed: {ex.Message}", "ArchiveHelper", ex);
            throw new InvalidOperationException($"Archive extraction failed: {ex.Message}", ex);
        }
    }

    /// <summary>Largest file we'll scan for an embedded (polyglot) archive — mods are tens of MB;
    /// don't read a multi-GB file into memory hunting a signature.</summary>
    private const long MaxCarveScanBytes = 600L * 1024 * 1024;

    /// <summary>
    /// A "polyglot" is a real file (e.g. mp4) with an archive APPENDED. Its archive offsets are
    /// relative to the archive's own start, so a whole-file reader rejects it. Find the FIRST archive
    /// signature at a non-zero offset and copy [offset..end] to a temp file (a clean standalone
    /// archive). Returns the temp path, or null if no embedded archive is found. Caller deletes it.
    /// </summary>
    private string? TryCarveEmbeddedArchive(string archivePath)
    {
        try
        {
            var info = new FileInfo(archivePath);
            if (!info.Exists || info.Length > MaxCarveScanBytes) return null;
            var bytes = File.ReadAllBytes(archivePath);

            // Signatures we can extract from. ZIP is scanned from the FIRST local-file header.
            ReadOnlySpan<byte> zip = [0x50, 0x4B, 0x03, 0x04];
            ReadOnlySpan<byte> sevenZip = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
            ReadOnlySpan<byte> rar = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07];

            var best = -1;
            foreach (var sig in new[] { zip.ToArray(), sevenZip.ToArray(), rar.ToArray() })
            {
                var at = IndexOf(bytes, sig, 1); // start at 1 — offset 0 was already tried whole-file
                if (at >= 0 && (best < 0 || at < best)) best = at;
            }
            if (best <= 0) return null;

            // Carve next to the source (same volume, not OS temp — the caller's staging dir owns it).
            var temp = Path.Combine(Path.GetDirectoryName(archivePath) ?? ".", $"carve-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(temp, bytes.AsSpan(best).ToArray());
            return temp;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Polyglot carve scan failed: {ex.Message}", "ArchiveHelper");
            return null;
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = Math.Max(0, start); i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>Map a magic-byte detected type to a SharpSevenZip <see cref="InArchiveFormat"/> so a
    /// disguised/extension-less file can still be extracted. Null = let SharpSevenZip guess by extension.</summary>
    private static InArchiveFormat? ToInArchiveFormat(string? detectedType) => detectedType switch
    {
        "zip" => InArchiveFormat.Zip,
        "7z" => InArchiveFormat.SevenZip,
        "rar" => InArchiveFormat.Rar,
        "gz" => InArchiveFormat.GZip,
        "bz2" => InArchiveFormat.BZip2,
        "tar" => InArchiveFormat.Tar,
        _ => null,
    };

    /// <summary>Cheap magic-byte test: is this file an archive we can extract (regardless of its
    /// extension)? Used by the recursive unwrap to find nested archives without trusting names.</summary>
    public bool IsArchiveFile(string path)
    {
        try { return ToInArchiveFormat(DetectArchiveTypeAsync(path).GetAwaiter().GetResult()) != null; }
        catch { return false; }
    }

    /// <summary>
    /// Extract an archive AND recursively unwrap any nested archives inside it, until only real
    /// content remains (or <paramref name="maxDepth"/> is hit). Mods are sometimes wrapped in
    /// multiple layers of zip — and some sites disguise the outer layer with a fake extension — so
    /// the import pipeline always verifies the extracted tree instead of trusting one extract.
    /// Detection is by MAGIC BYTES, so a nested "x.mp4" that is really a zip is still unwrapped.
    /// <paramref name="password"/> (if any) is tried on each layer only when a plain extract fails
    /// with a password error. Returns the number of archive layers unwrapped (>=1).
    /// </summary>
    /// <summary>Junk files a wrapper layer may carry beside the payload archive (promo links, readmes)
    /// — they must not count as "real content" when deciding whether a layer is just a wrapper.</summary>
    private static readonly HashSet<string> TrivialWrapperExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".url", ".txt", ".md", ".nfo", ".htm", ".html" };

    public async Task<int> ExtractArchiveRecursiveAsync(string archivePath, string targetDirectory, string? password = null, int maxDepth = 8)
    {
        Directory.CreateDirectory(targetDirectory);
        var parent = Directory.GetParent(targetDirectory)!.FullName;
        var work = Path.Combine(parent, $"unwrap-0-{Guid.NewGuid():N}");
        await ExtractWithOptionalPasswordAsync(archivePath, work, password).ConfigureAwait(false);
        var layers = 1;

        for (var depth = 1; depth <= maxDepth; depth++)
        {
            var files = Directory.EnumerateFiles(work, "*", SearchOption.AllDirectories).ToList();
            var archives = files.Where(IsArchiveFile).ToList();
            var hasRealContent = files.Any(f => !IsArchiveFile(f)
                && !TrivialWrapperExtensions.Contains(Path.GetExtension(f)));
            // Real content present (or nothing left to unwrap) → this layer IS the mod. Stop.
            if (archives.Count == 0 || hasRealContent) break;

            // Pure wrapper layer (only nested archive(s) + junk) → descend: extract them into a fresh
            // dir, discard this layer. Multiple archives extract into per-name subfolders (rare); the
            // common huihui case is one archive → its content lands at the next layer's root (flat).
            var next = Path.Combine(parent, $"unwrap-{depth}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(next);
            var single = archives.Count == 1;
            foreach (var inner in archives)
            {
                var into = single ? next : Path.Combine(next, Path.GetFileNameWithoutExtension(inner));
                await ExtractWithOptionalPasswordAsync(inner, into, password).ConfigureAwait(false);
                layers++;
            }
            try { Directory.Delete(work, recursive: true); } catch { }
            work = next;
        }

        // Move the final (real-content) tree into targetDirectory, then drop the working dir.
        MergeDirectory(work, targetDirectory);
        try { Directory.Delete(work, recursive: true); } catch { }
        _logger.Info($"Recursive extract: {layers} archive layer(s) unwrapped from {Path.GetFileName(archivePath)}", "ArchiveHelper");
        return layers;
    }

    /// <summary>Move every file from <paramref name="from"/> into <paramref name="to"/>, preserving
    /// relative subpaths (same-volume move; falls back to copy across volumes).</summary>
    private static void MergeDirectory(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, file);
            var dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(file, dest);
        }
    }

    /// <summary>Extract trying NO password first; on a password-suspect failure, retry with the
    /// supplied password (if any). Throws if both fail (or no password to try).</summary>
    private async Task ExtractWithOptionalPasswordAsync(string archivePath, string targetDirectory, string? password)
    {
        try
        {
            await ExtractArchiveAsync(archivePath, targetDirectory).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPasswordError(ex) && !string.IsNullOrEmpty(password))
        {
            try { if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, recursive: true); } catch { }
            await ExtractArchiveAsync(archivePath, targetDirectory, password).ConfigureAwait(false);
        }
    }

    /// <summary>Whether an extraction failure is password-SUSPECT. 7z reports a missing/wrong
    /// password on AES-encrypted data as "File is corrupted. Data error has occured." (the CRC check
    /// fails — indistinguishable from real corruption by design, verified in tests), so data-error
    /// messages count too. Callers retry with a password; if that also fails, surface both
    /// possibilities (wrong password OR corrupt file).</summary>
    public static bool IsPasswordError(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("data error", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("corrupted", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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
    /// Default: 7Z format for best compression ratio
    /// </summary>
    public async Task<string> CompressFolderAsync(
        string folderPath,
        string outputPath,
        ArchiveFormat format = ArchiveFormat.SevenZip,
        CompressionLevel compressionLevel = CompressionLevel.High,
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
                    _ => OutArchiveFormat.SevenZip  // Default to 7Z for best compression
                },
                // Use configured compression level from profile settings
                CompressionLevel = compressionLevel,
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

    /// <summary>
    /// Replace/add a single entry in an existing 7z archive via append mode — only the one file is
    /// (re)compressed; the rest of the archive's streams are copied. The entry key must match the
    /// existing in-archive path (forward slashes) so it replaces rather than duplicates.
    /// </summary>
    public async Task UpdateFileInArchiveAsync(string archivePath, string sourceFilePath, string entryPath)
    {
        if (!File.Exists(archivePath)) throw new FileNotFoundException($"Archive not found: {archivePath}");
        if (!File.Exists(sourceFilePath)) throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

        var normalizedEntry = entryPath.Replace('\\', '/').TrimStart('/');

        await Task.Run(() =>
        {
            InitializeSevenZip();
            var compressor = new SharpSevenZipCompressor
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                CompressionMode = CompressionMode.Append, // update the existing archive in place
                CompressionLevel = CompressionLevel.High,
                PreserveDirectoryRoot = false,
            };
            try
            {
                // key = path inside the archive, value = file on disk. Append replaces the matching entry.
                compressor.CompressFileDictionary(
                    new Dictionary<string, string> { { normalizedEntry, sourceFilePath } },
                    archivePath);
                _logger.Info($"Updated archive entry '{normalizedEntry}' in {Path.GetFileName(archivePath)}", "ArchiveHelper");
            }
            catch (Exception ex)
            {
                _logger.Error($"Single-file archive update failed: {ex.Message}", "ArchiveHelper", ex);
                throw new InvalidOperationException($"Failed to update file in archive: {ex.Message}", ex);
            }
        }).ConfigureAwait(false);
    }
}
