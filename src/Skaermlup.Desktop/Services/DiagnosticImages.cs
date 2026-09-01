using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Gemmer et billede af luppen og vejledningsbjælken som de ser ud lige nu. Luppen er skjult
/// for al skærmkopiering, så den kan ikke fotograferes udefra — men vinduet kan tegne sig selv.
/// Bruges til support: "send lige et prøvebillede, så kan jeg se hvad du ser".
/// </summary>
internal static class DiagnosticImages
{
    public static IReadOnlyList<string> SaveTo(string folder, params Window?[] windows)
    {
        Directory.CreateDirectory(folder);
        var saved = new List<string>();

        foreach (var window in windows)
        {
            if (window is null || !window.IsVisible) continue;

            string path = Path.Combine(folder, Sanitize(window.GetType().Name) + ".png");
            if (Save(window, path)) saved.Add(path);
        }

        return saved;
    }

    private static bool Save(Window window, string path)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        int width = (int)Math.Round(window.ActualWidth * dpi.DpiScaleX);
        int height = (int)Math.Round(window.ActualHeight * dpi.DpiScaleY);
        if (width <= 0 || height <= 0) return false;

        var target = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        target.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));

        using var stream = File.Create(path);
        encoder.Save(stream);
        return true;
    }

    private static string Sanitize(string name)
        => string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
}
