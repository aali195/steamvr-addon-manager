using System.Text;

namespace SteamVrAddonManager.Services;

/// <summary>Shared helpers for persisting configuration files without corrupting them.</summary>
internal static class FileStorage
{
    private const int BackupsToKeep = 5;

    /// <summary>
    /// Copies <paramref name="path"/> to a timestamped <c>.bak</c> beside it and returns the backup
    /// path, or <c>null</c> when the file does not exist. Older backups are pruned.
    /// </summary>
    public static string? Backup(string path)
    {
        if (!File.Exists(path))
            return null;

        var backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        File.Copy(path, backup, overwrite: true);
        PruneBackups(path);
        return backup;
    }

    /// <summary>Writes UTF-8 (no BOM) to a temp file, then moves it over the target atomically.</summary>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Best-effort check of whether files can be written into <paramref name="directory"/>.</summary>
    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return false;

            var probe = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void PruneBackups(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return;

            // Timestamped names sort chronologically, so keep the newest N.
            var stale = Directory.EnumerateFiles(directory, Path.GetFileName(path) + ".*.bak")
                .OrderByDescending(static file => file, StringComparer.OrdinalIgnoreCase)
                .Skip(BackupsToKeep);

            foreach (var file in stale)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // A locked backup is not worth failing the save over.
                }
            }
        }
        catch
        {
            // Pruning is best-effort.
        }
    }
}
