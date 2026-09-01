namespace Skaermlup.Core.Colors;

/// <summary>
/// Kopierer et skærmudsnit over i visningsbufferen og farvebehandler det undervejs.
/// Kilde og mål er begge 32 bit pr. pixel i rækkefølgen blå, grøn, rød, ubrugt.
/// </summary>
public static class ColorPipeline
{
    public const int BytesPerPixel = 4;

    public static void Apply(
        ReadOnlySpan<byte> source, int sourceStride,
        Span<byte> destination, int destinationStride,
        int width, int height, ColorMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (width <= 0 || height <= 0) return;

        int rowBytes = width * BytesPerPixel;
        if (sourceStride < rowBytes || destinationStride < rowBytes)
            throw new ArgumentException("Rækkelængden er mindre end en billedrække.", nameof(sourceStride));
        if (source.Length < (height - 1) * sourceStride + rowBytes)
            throw new ArgumentException("Kildebufferen er for lille.", nameof(source));
        if (destination.Length < (height - 1) * destinationStride + rowBytes)
            throw new ArgumentException("Målbufferen er for lille.", nameof(destination));

        if (map.IsIdentity)
        {
            for (int y = 0; y < height; y++)
                source.Slice(y * sourceStride, rowBytes)
                      .CopyTo(destination.Slice(y * destinationStride, rowBytes));
            return;
        }

        byte[] blue = map.Blue, green = map.Green, red = map.Red;

        for (int y = 0; y < height; y++)
        {
            var src = source.Slice(y * sourceStride, rowBytes);
            var dst = destination.Slice(y * destinationStride, rowBytes);

            if (map.UsesLuminance)
            {
                for (int x = 0; x < rowBytes; x += BytesPerPixel)
                {
                    // Rec. 601-vægtning i heltal: den opfattede lysstyrke, ikke et rent gennemsnit.
                    int lum = (src[x + 2] * 77 + src[x + 1] * 150 + src[x] * 29) >> 8;
                    dst[x] = blue[lum];
                    dst[x + 1] = green[lum];
                    dst[x + 2] = red[lum];
                    dst[x + 3] = 255;
                }
            }
            else
            {
                for (int x = 0; x < rowBytes; x += BytesPerPixel)
                {
                    dst[x] = blue[src[x]];
                    dst[x + 1] = green[src[x + 1]];
                    dst[x + 2] = red[src[x + 2]];
                    dst[x + 3] = 255;
                }
            }
        }
    }
}
