namespace Skaermlup.Core.Magnification;

/// <summary>
/// Fjerner de farvede kanter som Windows lægger langs bogstaver.
///
/// Windows tegner tekst med ClearType: en skærmpixel består af tre lodrette striber i rødt,
/// grønt og blåt, og ved at tænde dem forskelligt kan en bogstavkant placeres en tredjedel
/// pixel ad gangen. På skærmens egen opløsning smelter striberne sammen for øjet, og teksten
/// ser bare skarpere ud. Forstørret ti gange gør de ikke: så bliver hver stribe til en bred
/// rød eller blå bræmme langs bogstavet, og ved AMD, hvor farvesynet i forvejen er svækket,
/// er det ren støj oven i det man prøver at læse.
///
/// Farven jævnes derfor ud på tværs af de tre striber, mens lysstyrken bliver rørt. Det er
/// præcis den modsatte regnestykke af ClearType selv: bogstavets kant bliver liggende hvor
/// den er, med den nøjagtighed ClearType gav den, men den bliver grå igen. En rigtig rød
/// knap beholder sin farve, for dens farve er den samme over mange pixels i træk.
/// </summary>
public static class SubpixelFringe
{
    public const int BytesPerPixel = 4;

    /// <summary>Udjævner farven vandret i billedet uden at ændre lysstyrken. Ændrer bufferen.</summary>
    public static void Remove(Span<byte> pixels, int stride, int width, int height)
    {
        if (width < 3 || height < 1) return;
        if (stride < width * BytesPerPixel)
            throw new ArgumentException("Rækkelængden er mindre end en billedrække.", nameof(stride));
        if (pixels.Length < (height - 1) * stride + width * BytesPerPixel)
            throw new ArgumentException("Bufferen er for lille.", nameof(pixels));

        for (int y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width * BytesPerPixel);

            // Naboen til venstre huskes, fordi den allerede er skrevet over. Naboen til
            // højre kan læses direkte, for den er ikke nået at blive ændret endnu.
            (int blue, int green, int red, int luma) previous = Read(row, 0);

            for (int x = 0; x < width; x++)
            {
                int offset = x * BytesPerPixel;
                var here = Read(row, offset);
                var next = Read(row, x < width - 1 ? offset + BytesPerPixel : offset);

                int blue = (previous.blue - previous.luma) + (here.blue - here.luma) + (next.blue - next.luma);
                int green = (previous.green - previous.luma) + (here.green - here.luma) + (next.green - next.luma);
                int red = (previous.red - previous.luma) + (here.red - here.luma) + (next.red - next.luma);

                row[offset] = Clamp(here.luma + Round(blue));
                row[offset + 1] = Clamp(here.luma + Round(green));
                row[offset + 2] = Clamp(here.luma + Round(red));

                previous = here;
            }
        }
    }

    private static (int blue, int green, int red, int luma) Read(ReadOnlySpan<byte> row, int offset)
    {
        int blue = row[offset];
        int green = row[offset + 1];
        int red = row[offset + 2];
        return (blue, green, red, (red * 77 + green * 150 + blue * 29) >> 8);
    }

    /// <summary>Gennemsnittet af tre farveafvigelser, afrundet mod nul så grå forbliver grå.</summary>
    private static int Round(int sumOfThree) => sumOfThree >= 0 ? (sumOfThree + 1) / 3 : (sumOfThree - 1) / 3;

    private static byte Clamp(int value) => value <= 0 ? (byte)0 : value >= 255 ? (byte)255 : (byte)value;
}
