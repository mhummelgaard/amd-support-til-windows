namespace Skaermlup.Core.Geometry;

/// <summary>Et punkt i fysiske skærmpixels (koordinat på det virtuelle skrivebord).</summary>
public readonly record struct PixelPoint(int X, int Y)
{
    public static PixelPoint Zero => new(0, 0);

    public int DistanceSquaredTo(PixelPoint other)
    {
        long dx = X - other.X;
        long dy = Y - other.Y;
        long d = dx * dx + dy * dy;
        return d > int.MaxValue ? int.MaxValue : (int)d;
    }
}

/// <summary>En størrelse i fysiske skærmpixels.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>Et rektangel i fysiske skærmpixels. Højre kant og bundkant er eksklusive.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static PixelRect Empty => new(0, 0, 0, 0);

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public PixelSize Size => new(Width, Height);
    public PixelPoint Center => new(X + Width / 2, Y + Height / 2);

    public static PixelRect FromBounds(int left, int top, int right, int bottom)
        => new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));

    /// <summary>
    /// Flytter rektanglet ind i containeren uden at ændre dets størrelse. Er rektanglet
    /// større end containeren, lægges det op i containerens øverste venstre hjørne.
    /// </summary>
    public PixelRect ClampInto(PixelRect container)
    {
        int x = Width >= container.Width
            ? container.X
            : Math.Clamp(X, container.X, container.Right - Width);
        int y = Height >= container.Height
            ? container.Y
            : Math.Clamp(Y, container.Y, container.Bottom - Height);
        return new PixelRect(x, y, Width, Height);
    }

    /// <summary>Et rektangel med den givne størrelse centreret om et punkt.</summary>
    public static PixelRect CenteredOn(PixelPoint center, PixelSize size)
        => new(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);

    public override string ToString() => $"{Width}x{Height} ved ({X}, {Y})";
}
