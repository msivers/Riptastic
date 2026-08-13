namespace Riptastic.Models;

/// <summary>A single external tool/library the ripper relies on.</summary>
public sealed class DependencyResult
{
    public required string Name { get; init; }
    public required string Purpose { get; init; }
    public required string InstallCommand { get; init; }

    public bool Found { get; set; }
    public string? ResolvedPath { get; set; }

    public string StatusGlyph => Found ? "✓" : "✗";
    public string StatusColor => Found ? "#3FB950" : "#E23B54";
    public string DetailText => Found
        ? $"Found: {ResolvedPath}"
        : $"Missing - install with:  {InstallCommand}";
}
