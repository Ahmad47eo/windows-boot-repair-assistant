namespace BootRepairAssistant2.Core;

public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string dir, string pattern);

    void CreateDirectory(string path);

    void CopyFile(string src, string dst, bool overwrite);

    long GetFileLength(string path);

    void WriteAllText(string path, string text);

    void DeleteFile(string path);
}
