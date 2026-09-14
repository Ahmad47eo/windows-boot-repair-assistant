namespace BootRepairAssistant2.Core;

public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public IEnumerable<string> EnumerateFiles(string dir, string pattern)
    {
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, pattern)
            : Array.Empty<string>();
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void CopyFile(string src, string dst, bool overwrite)
    {
        File.Copy(src, dst, overwrite);
    }

    public long GetFileLength(string path)
    {
        return new FileInfo(path).Length;
    }

    public void WriteAllText(string path, string text)
    {
        File.WriteAllText(path, text);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }
}
