using System.Diagnostics;

namespace Riptastic.Services;

/// <summary>
/// Runs external tools with Homebrew paths on PATH/DYLD - a Finder-launched .app gets a
/// minimal PATH without /opt/homebrew, yet libdvdnav must dlopen libdvdcss from there.
/// </summary>
public static class ProcessRunner
{
    private static readonly string[] BinDirs = ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin"];
    private static readonly string[] LibDirs = ["/opt/homebrew/lib", "/usr/local/lib"];

    public static Process Create(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        var basePath = string.Join(':', BinDirs);
        var existingPath = Environment.GetEnvironmentVariable("PATH");
        psi.Environment["PATH"] = string.IsNullOrEmpty(existingPath) ? basePath : $"{basePath}:{existingPath}";

        var baseLib = string.Join(':', LibDirs);
        var existingLib = Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH");
        psi.Environment["DYLD_LIBRARY_PATH"] = string.IsNullOrEmpty(existingLib) ? baseLib : $"{baseLib}:{existingLib}";

        return new Process { StartInfo = psi, EnableRaisingEvents = true };
    }
}
