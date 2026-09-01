using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Magnification;

/// <summary>
/// Regner ud hvad luppen skal vise. Ren matematik uden Windows-afhængigheder, så
/// kanttilfældene (skærmhjørner, ekstrem forstørrelse, meget små skærme) kan efterprøves.
/// </summary>
public static class MagnifierGeometry
{
    /// <summary>Mindste fornuftige lup. Under dette kan et helt ord ikke stå i vinduet.</summary>
    public const int MinLensPx = 140;

    /// <summary>Mindste højde på læsebåndet.</summary>
    public const int MinDockPx = 90;

    public static PixelSize LensSizeFor(PixelRect monitor, int widthPercent, int heightPercent, LensShape shape)
    {
        if (monitor.IsEmpty) return new PixelSize(0, 0);

        int w = monitor.Width * Math.Clamp(widthPercent, 10, 100) / 100;
        int h = monitor.Height * Math.Clamp(heightPercent, 10, 100) / 100;

        w = Math.Clamp(w, Math.Min(MinLensPx, monitor.Width), monitor.Width);
        h = Math.Clamp(h, Math.Min(MinLensPx, monitor.Height), monitor.Height);

        if (shape == LensShape.Circle)
        {
            int d = Math.Min(w, h);
            w = d;
            h = d;
        }

        return new PixelSize(w, h);
    }

    /// <summary>Luppen som forstørrelsesglas: vinduet ligger oven på fokuspunktet.</summary>
    public static MagnifierFrame ComputeLensFrame(PixelPoint focus, PixelRect monitor, PixelSize lensSize, double zoom)
    {
        if (monitor.IsEmpty || lensSize.IsEmpty) return default;

        zoom = ZoomLadder.Clamp(zoom);
        var view = PixelRect.CenteredOn(focus, lensSize).ClampInto(monitor);
        var source = SourceFor(focus, lensSize, zoom, monitor);
        return Frame(source, view, focus);
    }

    /// <summary>Læsebånd: en fast stribe i toppen eller bunden, der viser området omkring fokus.</summary>
    public static MagnifierFrame ComputeDockFrame(PixelPoint focus, PixelRect monitor, DockEdge edge, int heightPercent, double zoom)
    {
        if (monitor.IsEmpty) return default;

        zoom = ZoomLadder.Clamp(zoom);

        int bandHeight = monitor.Height * Math.Clamp(heightPercent, 10, 90) / 100;
        bandHeight = Math.Clamp(bandHeight, Math.Min(MinDockPx, monitor.Height), monitor.Height);

        var view = edge == DockEdge.Top
            ? new PixelRect(monitor.X, monitor.Y, monitor.Width, bandHeight)
            : new PixelRect(monitor.X, monitor.Bottom - bandHeight, monitor.Width, bandHeight);

        // Kilden holdes uden for båndet, så luppen aldrig kommer til at forstørre sig selv.
        var free = edge == DockEdge.Top
            ? PixelRect.FromBounds(monitor.X, view.Bottom, monitor.Right, monitor.Bottom)
            : PixelRect.FromBounds(monitor.X, monitor.Y, monitor.Right, view.Y);
        if (free.IsEmpty) free = monitor;

        var source = SourceFor(focus, view.Size, zoom, free);
        return Frame(source, view, focus);
    }

    private static PixelRect SourceFor(PixelPoint focus, PixelSize viewSize, double zoom, PixelRect bounds)
    {
        int sw = Math.Max(1, (int)Math.Round(viewSize.Width / zoom));
        int sh = Math.Max(1, (int)Math.Round(viewSize.Height / zoom));
        sw = Math.Min(sw, bounds.Width);
        sh = Math.Min(sh, bounds.Height);
        return PixelRect.CenteredOn(focus, new PixelSize(sw, sh)).ClampInto(bounds);
    }

    private static MagnifierFrame Frame(PixelRect source, PixelRect view, PixelPoint focus)
    {
        if (source.IsEmpty || view.IsEmpty) return default;

        double sx = (double)view.Width / source.Width;
        double sy = (double)view.Height / source.Height;

        // Fokus kan ligge uden for kilden når musen står helt ude i en skærmkant. Markøren
        // klemmes derfor ind i visningen i stedet for at forsvinde ud over kanten.
        int mx = (int)Math.Round((focus.X - source.X) * sx);
        int my = (int)Math.Round((focus.Y - source.Y) * sy);
        mx = Math.Clamp(mx, 0, view.Width);
        my = Math.Clamp(my, 0, view.Height);

        return new MagnifierFrame(source, view, sx, sy, new PixelPoint(mx, my));
    }
}
