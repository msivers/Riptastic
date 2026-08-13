namespace Riptastic.Models;

/// <summary>One title on the DVD, as reported by a HandBrake JSON scan.</summary>
public sealed class DvdTitle
{
    public int Index { get; init; }              // HandBrake title number (1-based)
    public TimeSpan Duration { get; init; }
    public int Width { get; init; }              // stored pixel width  (e.g. 720)
    public int Height { get; init; }             // stored pixel height (e.g. 576)
    public int ParNum { get; init; } = 1;        // pixel aspect ratio numerator
    public int ParDen { get; init; } = 1;        // pixel aspect ratio denominator

    // Auto-detected black-bar crop: top, bottom, left, right.
    public int CropTop { get; init; }
    public int CropBottom { get; init; }
    public int CropLeft { get; init; }
    public int CropRight { get; init; }

    public int AudioCount { get; init; }
    public int SubtitleCount { get; init; }
    public int ChapterCount { get; init; }
    public bool IsMainFeature { get; set; }

    /// <summary>Non-square pixels - the frame is stored at one size but meant to display at another.</summary>
    public bool IsAnamorphic => ParNum != ParDen;

    /// <summary>Width the whole frame is meant to display at, with square pixels (even number).</summary>
    public int DisplayWidth => MakeEven(Width * (double)ParNum / ParDen);
    public int DisplayHeight => Height;

    /// <summary>Aspect ratio of the full stored frame (the DVD's 16:9 or 4:3 flag).</summary>
    public string FrameAspectText => NameRatio((double)DisplayWidth / DisplayHeight);

    // --- Actual picture content, after black bars are cropped off ---------

    private int ContentStoredWidth => Math.Max(2, Width - CropLeft - CropRight);
    private int ContentStoredHeight => Math.Max(2, Height - CropTop - CropBottom);
    private double ContentAspect =>
        (ContentStoredWidth * (double)ParNum / ParDen) / ContentStoredHeight;

    /// <summary>Aspect ratio of the visible image once letterbox bars are removed.</summary>
    public string ContentAspectText => NameRatio(ContentAspect);

    /// <summary>True when the picture is letterboxed inside the frame (e.g. a 2.35:1 film in a 16:9 frame).</summary>
    public bool IsLetterboxed => (CropTop + CropBottom) >= 40;

    /// <summary>True when the picture is wider than 16:9 (a "scope" film that can be side-cropped to fill 16:9).</summary>
    public bool IsScope => ContentAspect >= 1.90;

    /// <summary>Picture-content line shown under the title picker.</summary>
    public string ContentSummary => IsLetterboxed
        ? $"Picture is {ContentAspectText} - plays letterboxed on a 16:9 screen (black bars removed on rip)"
        : $"Picture is {ContentAspectText} - fills a 16:9 screen";

    /// <summary>Crop [top, bottom, left, right] that trims the bars and sides to fill 16:9 (even values, as HandBrake requires).</summary>
    public (int Top, int Bottom, int Left, int Right) Fill16x9Crop()
    {
        int top = MakeEvenDown(CropTop), bottom = MakeEvenDown(CropBottom);
        int contentH = Math.Max(2, Height - top - bottom);
        // Stored width whose square-pixel display aspect is exactly 16:9.
        int targetW = (int)Math.Round(16.0 / 9 * contentH * ParDen / (double)ParNum);
        int contentW = Math.Max(2, Width - CropLeft - CropRight);
        int sideEach = MakeEvenDown(Math.Max(0, contentW - targetW) / 2);
        return (top, bottom, CropLeft + sideEach, CropRight + sideEach);
    }

    public string Display =>
        $"Title {Index}  ·  {Duration:hh\\:mm\\:ss}  ·  {ContentAspectText}  ·  " +
        $"{ChapterCount} ch, {AudioCount} audio, {SubtitleCount} subs" +
        (IsMainFeature ? "   ● main feature" : string.Empty);

    // --- helpers ----------------------------------------------------------

    private static int MakeEven(double v)
    {
        var w = (int)Math.Round(v);
        return w % 2 == 0 ? w : w + 1;
    }

    private static int MakeEvenDown(int v) => v - (v % 2);

    /// <summary>Snaps common ratios to friendly names; otherwise gives a decimal like "2.35:1".</summary>
    private static string NameRatio(double r)
    {
        if (Math.Abs(r - 16.0 / 9) < 0.12) return "16:9";   // absorbs minor overscan (e.g. 1.85)
        if (Math.Abs(r - 4.0 / 3) < 0.10) return "4:3";
        return $"{r:0.00}:1";
    }
}
