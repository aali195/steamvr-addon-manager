using System.IO;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.Tests;

public sealed class FileStorageTests
{
    [Fact]
    public void IsDirectoryWritable_TrueForTempDirectory()
    {
        using var dir = new TempDir();
        Assert.True(FileStorage.IsDirectoryWritable(dir.Path));
    }

    [Fact]
    public void IsDirectoryWritable_FalseForMissingDirectory()
    {
        Assert.False(FileStorage.IsDirectoryWritable(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void Backup_PrunesToFiveMostRecent()
    {
        using var dir = new TempDir();
        var settings = dir.File("steamvr.vrsettings", "{}");

        var start = new DateTime(2026, 1, 1);
        for (var i = 0; i < 15; i++)
            File.WriteAllText($"{settings}.{start.AddSeconds(i):yyyyMMdd-HHmmss}.bak", "old");

        FileStorage.Backup(settings);

        Assert.Equal(5, Directory.GetFiles(dir.Path, "steamvr.vrsettings.*.bak").Length);
    }

    [Fact]
    public void WriteAllTextAtomic_WritesContentWithoutBom()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "out.txt");

        FileStorage.WriteAllTextAtomic(path, "hello");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal("hello", File.ReadAllText(path));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }
}
