using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Setting.Models;

namespace D3dxSkinManager.Modules.Setting.Services;

/// <summary>
/// Service for managing language/i18n files
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Get language file by code (e.g., "en", "cn")
    /// </summary>
    Task<LanguageSettings?> GetLanguageAsync(string languageCode);

    /// <summary>
    /// Save language file
    /// </summary>
    Task SaveLanguageAsync(LanguageSettings language);
}

/// <summary>
/// Service for managing language/i18n files
/// Language files are stored in data/languages/ as {code}.json (e.g., en.json, cn.json)
/// </summary>
public class LanguageService : ILanguageService
{
    private readonly string _languagesDirectory;
    private readonly ILogHelper _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LanguageService(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var globalPathsService = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));

        // Shipped language files live under res/languages (sibling of libs/, not user data/).
        _languagesDirectory = globalPathsService.LanguagesDirectory;

        // Ensure languages directory exists
        Directory.CreateDirectory(_languagesDirectory);
        _logger.Info($"Languages directory: {_languagesDirectory}", "LanguageService");
    }

    /// <summary>
    /// Get language file by code
    /// </summary>
    public async Task<LanguageSettings?> GetLanguageAsync(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Language code cannot be empty", nameof(languageCode));
        }

        // Validate language code (prevent path traversal)
        if (languageCode.Contains("..") || languageCode.Contains("/") || languageCode.Contains("\\"))
        {
            throw new ArgumentException($"Invalid language code: {languageCode}", nameof(languageCode));
        }

        var filePath = GetLanguageFilePath(languageCode);

        if (!File.Exists(filePath))
        {
            _logger.Warn($"Language file not found: {languageCode}", "LanguageService");
            return null;
        }

        try
        {
            var language = await JsonHelper.DeserializeFromFileAsync<LanguageSettings>(filePath);
            _logger.Info($"Loaded language: {languageCode}", "LanguageService");
            return language;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load language {languageCode}: {ex.Message}", "LanguageService");
            throw;
        }
    }

    /// <summary>
    /// Save language file
    /// </summary>
    public async Task SaveLanguageAsync(LanguageSettings language)
    {
        if (language == null)
        {
            throw new ArgumentNullException(nameof(language));
        }

        if (string.IsNullOrWhiteSpace(language.Code))
        {
            throw new ArgumentException("Language code cannot be empty", nameof(language));
        }

        // Validate language code
        if (language.Code.Contains("..") || language.Code.Contains("/") || language.Code.Contains("\\"))
        {
            throw new ArgumentException($"Invalid language code: {language.Code}", nameof(language));
        }

        var filePath = GetLanguageFilePath(language.Code);

        try
        {
            await JsonHelper.SerializeToFileAsync(filePath, language).ConfigureAwait(false);
            _logger.Info($"Saved language: {language.Code}", "LanguageService");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save language {language.Code}: {ex.Message}", "LanguageService");
            throw;
        }
    }

    /// <summary>
    /// Get full file path for language code
    /// </summary>
    private string GetLanguageFilePath(string languageCode)
    {
        return Path.Combine(_languagesDirectory, $"{languageCode}.json");
    }
}
