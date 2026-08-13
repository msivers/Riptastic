namespace Riptastic.Models;

public enum RipStage { Idle, Scanning, Encoding, Muxing, Verifying, Done, Cancelled, Failed }

/// <summary>Quality preset exposed in the UI, mapped to an x264 RF (constant quality) value.</summary>
public sealed record QualityOption(string Label, int Rf, string Blurb)
{
    public override string ToString() => Label;

    public static readonly QualityOption[] All =
    [
        new("High - near source (RF 18)",   18, "Largest files, visually indistinguishable from the DVD."),
        new("Balanced (RF 20)",             20, "Great quality at a noticeably smaller size."),
        new("Smaller (RF 22)",              22, "Roughly half the size; minor softening in busy scenes."),
    ];
}

/// <summary>Native keeps the film's real ratio; Fill16x9 side-crops a scope film to fill 16:9.</summary>
public enum OutputShape { Native, Fill16x9 }

/// <summary>An output-shape choice shown in the UI (label depends on the selected title).</summary>
public sealed record OutputShapeOption(string Label, OutputShape Shape)
{
    public override string ToString() => Label;
}

/// <summary>Progress event surfaced by the rip pipeline.</summary>
public sealed record RipProgress(RipStage Stage, double Fraction, string Message, bool Indeterminate = false);
