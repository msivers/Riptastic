using Riptastic.Models;

namespace Riptastic.Services;

/// <summary>Locates the external tools the ripper needs, on macOS.</summary>
public static class DependencyChecker
{
    private static readonly string[] BinDirs = ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin"];
    private static readonly string[] LibDirs = ["/opt/homebrew/lib", "/usr/local/lib"];

    public static string? FindExecutable(string name)
    {
        foreach (var dir in BinDirs)
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static string? FindLibrary(string prefix)
    {
        foreach (var dir in LibDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var fn = Path.GetFileName(file);
                if (fn.StartsWith(prefix, StringComparison.Ordinal) && fn.EndsWith(".dylib", StringComparison.Ordinal))
                    return file;
            }
        }
        return null;
    }

    public static bool HomebrewInstalled() => FindExecutable("brew") is not null;

    public static List<DependencyResult> CheckAll()
    {
        var results = new List<DependencyResult>
        {
            new()
            {
                Name = "HandBrakeCLI",
                Purpose = "Reads the DVD and encodes the H.264 video (the MKV).",
                InstallCommand = "brew install handbrake",
            },
            new()
            {
                Name = "ffmpeg",
                Purpose = "Builds the MP4 (adds an AAC track and web fast-start).",
                InstallCommand = "brew install ffmpeg",
            },
            new()
            {
                Name = "ffprobe",
                Purpose = "Inspects and verifies the finished files.",
                InstallCommand = "brew install ffmpeg",
            },
            new()
            {
                Name = "libdvdcss",
                Purpose = "Decrypts commercial DVDs so their contents can be read.",
                InstallCommand = "brew install libdvdcss",
            },
        };

        foreach (var dep in results)
        {
            dep.ResolvedPath = dep.Name == "libdvdcss"
                ? FindLibrary("libdvdcss")
                : FindExecutable(dep.Name);
            dep.Found = dep.ResolvedPath is not null;
        }

        return results;
    }
}
