using System;
using System.IO;
using System.Linq;

namespace ClaudeSessionManager.Wpf.Services;

public static class ClaudeProjectPathMapper
{
    public static string GetProjectsRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public static string EncodeCwd(string cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return string.Empty;
        var trimmed = cwd.TrimEnd('\\', '/');
        return trimmed.Replace(":\\", "--").Replace("\\", "-").Replace("/", "-");
    }

    public static string? ResolveProjectFolder(string cwd)
    {
        try
        {
            var root = GetProjectsRoot();
            if (!Directory.Exists(root)) return null;

            var encoded = EncodeCwd(cwd);
            if (string.IsNullOrEmpty(encoded)) return null;

            var dirs = Directory.EnumerateDirectories(root);
            return dirs.FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), encoded, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PathMapper] {ex}");
            return null;
        }
    }
}
