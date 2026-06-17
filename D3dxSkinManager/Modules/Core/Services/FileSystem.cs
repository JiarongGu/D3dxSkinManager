namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Thin abstraction over the raw file system operations used by the file-operation pipeline.
/// Layer-1 pure operations: no business logic, no events.
///
/// Exists so the FileOperationPlanner (and other "complex file processing jobs") can be driven
/// by an in-memory fake in tests that simulates latency and transient lock (IOException) errors,
/// which is impossible against the static System.IO API.
///
/// Only the operations the pipeline actually performs are exposed. Methods map 1:1 to System.IO
/// so the real implementation is a trivial forward.
/// </summary>
public interface IFileSystem
{
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive);
    void MoveDirectory(string sourcePath, string destinationPath);

    bool FileExists(string path);
    void DeleteFile(string path);
    void MoveFile(string sourcePath, string destinationPath);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);
}

/// <summary>
/// Real file system backed by System.IO. Stateless — safe as a singleton.
/// </summary>
public class SystemFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public void MoveDirectory(string sourcePath, string destinationPath) => Directory.Move(sourcePath, destinationPath);

    public bool FileExists(string path) => File.Exists(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) => File.Copy(sourcePath, destinationPath, overwrite);
}
