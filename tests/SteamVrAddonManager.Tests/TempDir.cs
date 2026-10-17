using System.IO;

namespace SteamVrAddonManager.Tests;

/// <summary>A disposable temporary directory for filesystem tests.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "svrtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name, string content)
    {
        var path = System.IO.Path.Combine(Path, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Test cleanup is best-effort.
        }
    }
}
