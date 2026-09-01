using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Skaermlup.Desktop;

/// <summary>Viser eller skjuler et element ud fra en sand/falsk-værdi.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
