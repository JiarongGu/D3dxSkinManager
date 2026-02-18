using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for mod archive operations
/// </summary>
public interface IModArchiveService
{
    Task<bool> LoadAsync(string sha);
    Task<bool> UnloadAsync(string sha);
    Task<bool> DeleteAsync(string sha, string? thumbnailPath, string? previewPath);
    bool ArchiveExists(string sha);
    string GetArchivePath(string sha);
    Task<string> CopyArchiveAsync(string sourcePath, string sha);
}
