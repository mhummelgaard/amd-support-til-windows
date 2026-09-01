using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Colors;

/// <summary>
/// Færdigberegnede opslagstabeller for én farvetilstand. Tabellerne bygges når indstillingen
/// ændres, ikke for hvert billede, så selve billedbehandlingen kun er ét opslag pr. farvekanal.
/// </summary>
public sealed class ColorMap
{
    private ColorMap(bool isIdentity, bool usesLuminance, byte[] blue, byte[] green, byte[] red)
    {
        IsIdentity = isIdentity;
        UsesLuminance = usesLuminance;
        Blue = blue;
        Green = green;
        Red = red;
    }

    /// <summary>Sandt når billedet kan kopieres uændret igennem.</summary>
    public bool IsIdentity { get; }

    /// <summary>
    /// Sandt når tabellerne slås op med pixlens lysstyrke i stedet for hver farvekanal for sig.
    /// Det er sådan tilstande som "gul på sort" laves.
    /// </summary>
    public bool UsesLuminance { get; }

    public byte[] Blue { get; }
    public byte[] Green { get; }
    public byte[] Red { get; }

    public static ColorMap Identity { get; } = BuildIdentity();

    private static ColorMap BuildIdentity()
    {
        var table = new byte[256];
        for (int i = 0; i < 256; i++) table[i] = (byte)i;
        return new ColorMap(true, false, table, table, table);
    }

    /// <param name="contrast">-50 til 50. 0 er skærmens egen kontrast.</param>
    /// <param name="brightness">-50 til 50. 0 er skærmens egen lysstyrke.</param>
    public static ColorMap Build(ColorScheme scheme, int contrast, int brightness)
    {
        contrast = Math.Clamp(contrast, -50, 50);
        brightness = Math.Clamp(brightness, -50, 50);

        if (scheme == ColorScheme.Normal && contrast == 0 && brightness == 0)
            return Identity;

        var pre = new byte[256];
        double factor = ContrastFactor(contrast * 3);
        double offset = brightness * 1.6;
        for (int i = 0; i < 256; i++)
            pre[i] = ToByte(factor * (i - 128.0) + 128.0 + offset);

        // Ekstra kontrastløft oven i brugerens egen indstilling.
        if (scheme == ColorScheme.HighContrast)
        {
            double boost = ContrastFactor(120);
            for (int i = 0; i < 256; i++)
                pre[i] = ToByte(boost * (pre[i] - 128.0) + 128.0);
        }

        switch (scheme)
        {
            case ColorScheme.Normal:
            case ColorScheme.HighContrast:
                return new ColorMap(false, false, pre, pre, pre);

            case ColorScheme.Inverted:
            {
                var inv = Map(pre, v => 255 - v);
                return new ColorMap(false, false, inv, inv, inv);
            }

            case ColorScheme.Grayscale:
                return new ColorMap(false, true, pre, pre, pre);

            case ColorScheme.WhiteOnBlack:
            {
                var inv = Map(pre, v => 255 - v);
                return new ColorMap(false, true, inv, inv, inv);
            }

            case ColorScheme.YellowOnBlack:
            {
                var lit = Map(pre, v => 255 - v);
                return new ColorMap(false, true, Zeros(), lit, lit);
            }

            case ColorScheme.BlackOnYellow:
                return new ColorMap(false, true, Zeros(), pre, pre);

            default:
                return Identity;
        }
    }

    private static byte[] Zeros() => new byte[256];

    private static byte[] Map(byte[] source, Func<int, int> transform)
    {
        var result = new byte[256];
        for (int i = 0; i < 256; i++) result[i] = ToByte(transform(source[i]));
        return result;
    }

    private static double ContrastFactor(double c)
        => 259.0 * (c + 255.0) / (255.0 * (259.0 - c));

    private static byte ToByte(double v)
        => v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5);
}
