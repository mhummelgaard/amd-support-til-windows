using System.Diagnostics;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;
using Skaermlup.Desktop.Capture;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Kører hele billedvejen igennem én gang ved opstart: hent et lille udsnit af skærmen,
/// farvebehandl det og tag tid på det. Svarene ender i statusfilen, så man kan se om det
/// overhovedet virker på maskinen — uden at skulle kunne se skærmen.
/// </summary>
internal static class SelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var lines = new List<string>();

        try
        {
            using var capture = new ScreenCapture();

            var screen = ScreenInfo.VirtualScreen();
            var source = new PixelRect(
                screen.X + screen.Width / 4,
                screen.Y + screen.Height / 4,
                Math.Min(240, screen.Width),
                Math.Min(180, screen.Height));

            var timer = Stopwatch.StartNew();
            bool captured = capture.Capture(source);
            timer.Stop();

            lines.Add("Skaermkopi: " + (captured ? "ok" : "MISLYKKEDES"));
            if (!captured) return lines;

            lines.Add("Tid for et billede: " + timer.Elapsed.TotalMilliseconds.ToString("0.00") + " ms");
            lines.Add("Forskellige tonetrin i proeven: " + DistinctTones(capture.Pixels)
                      + " (1 betyder et helt ensfarvet billede)");

            var map = ColorMap.Build(ColorScheme.Inverted, 0, 0, YellowTone.Pure);
            var destination = new byte[capture.Height * capture.Stride];

            timer.Restart();
            ColorPipeline.Apply(capture.Pixels, capture.Stride, destination, capture.Stride,
                capture.Width, capture.Height, map);
            timer.Stop();

            lines.Add("Tid for farvebehandling: " + timer.Elapsed.TotalMilliseconds.ToString("0.00") + " ms");
        }
        catch (Exception ex)
        {
            lines.Add("Selvtest gik galt: " + ex.Message);
        }

        return lines;
    }

    private static int DistinctTones(ReadOnlySpan<byte> pixels)
    {
        Span<bool> seen = stackalloc bool[256];
        int count = 0;

        for (int i = 0; i < pixels.Length; i += 251)
        {
            if (seen[pixels[i]]) continue;
            seen[pixels[i]] = true;
            count++;
        }

        return count;
    }
}
