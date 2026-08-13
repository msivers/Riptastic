using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Riptastic.Views;

/// <summary>true → green, false → red. Used for dependency status glyphs.</summary>
public sealed class FoundToBrushConverter : IValueConverter
{
    public static readonly FoundToBrushConverter Instance = new();

    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#3FB950"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#E23B54"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Ok : Bad;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
