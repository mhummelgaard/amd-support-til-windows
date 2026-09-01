namespace Skaermlup.Core.Magnification;

/// <summary>
/// Forstørrer et skærmudsnit uden at gøre bogstaverne udflydende.
///
/// Detaljer der aldrig blev optaget, kan ingen regnemetode finde frem. Men <em>måden</em> der
/// forstørres på afgør om teksten kan læses. Almindelig udglatning trækker kanten ud i en blød
/// overgang, der bliver bredere jo mere man forstørrer, så bogstaverne bliver slørede. Ren
/// firkantforstørrelse holder kanten skarp, men laver trappetrin på alle skrå og runde streger.
///
/// Her bruges begge egenskaber. Kantens <em>placering</em> findes ved almindelig udglatning —
/// den er nøjagtig langt ned under en hel pixel, og det er derfor kurver bliver bløde. Kantens
/// <em>stejlhed</em> genrejses bagefter: hver ny pixel måles op mod det lyseste og mørkeste i
/// kantens omegn og skubbes mod den ende, den ligger tættest på. Overgangen bliver derfor lige
/// så smal som på den oprindelige skærm, uanset forstørrelse, men bliver liggende hvor den var.
///
/// Det er den samme idé som bag skrifttyper tegnet ud fra afstandsfelter: gem hvor kanten er,
/// ikke hvordan den ser ud udtværet.
///
/// Klassen genbruger sine hjælpebuffere mellem billeder, så der ikke allokeres hukommelse
/// tres gange i sekundet. Den hører til ét lupvindue og må ikke deles mellem tråde.
/// </summary>
public sealed class EdgeUpscaler
{
    /// <summary>Fire bytes pr. pixel: blå, grøn, rød, ubrugt.</summary>
    public const int BytesPerPixel = 4;

    /// <summary>
    /// Under denne forstørrelse er der ikke noget at vinde: overgangen er stadig kun et par
    /// pixels bred, og grafikkortet klarer opgaven billigere.
    /// </summary>
    public const double MinimumUsefulZoom = 3.0;

    /// <summary>
    /// Under denne forskel mellem lyseste og mørkeste nabo er der ingen kant — kun en jævn
    /// flade eller lidt støj. Der røres ikke ved den slags; ellers ville billedstøj blive
    /// forstærket til pletter.
    /// </summary>
    private const int FlatThreshold = 12;

    private byte[] _luma = [];
    private byte[] _minima = [];
    private byte[] _maxima = [];
    private int[] _columnLeft = [];
    private int[] _columnRight = [];
    private float[] _columnWeight = [];

    public static bool IsWorthwhile(double zoom) => zoom >= MinimumUsefulZoom;

    /// <summary>
    /// Hvor hårdt overgangen strammes sammen. Målet er en kant der er cirka to skærmpixels
    /// bred uanset forstørrelse — smallere giver trappetrin, bredere ser udvasket ud.
    /// </summary>
    public static double SteepnessFor(double zoom) => Math.Clamp(zoom / 2.0, 1.0, 8.0);

    /// <summary>
    /// Skalerer kildeudsnittet op til den færdige størrelse. Kilde og mål er begge 32 bit
    /// pr. pixel i rækkefølgen blå, grøn, rød, ubrugt.
    /// </summary>
    public void Upscale(
        byte[] source, int sourceStride, int sourceWidth, int sourceHeight,
        byte[] destination, int destinationStride, int outWidth, int outHeight,
        double steepness)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (sourceWidth <= 0 || sourceHeight <= 0 || outWidth <= 0 || outHeight <= 0) return;

        if (sourceStride % BytesPerPixel != 0)
            throw new ArgumentException("Rækkelængden skal gå op i fire bytes pr. pixel.", nameof(sourceStride));
        if (sourceStride < sourceWidth * BytesPerPixel)
            throw new ArgumentException("Rækkelængden er mindre end en billedrække.", nameof(sourceStride));
        if (destinationStride < outWidth * BytesPerPixel)
            throw new ArgumentException("Målets rækkelængde er for lille.", nameof(destinationStride));
        if (source.Length < (sourceHeight - 1) * sourceStride + sourceWidth * BytesPerPixel)
            throw new ArgumentException("Kildebufferen er for lille.", nameof(source));
        if (destination.Length < (outHeight - 1) * destinationStride + outWidth * BytesPerPixel)
            throw new ArgumentException("Målbufferen er for lille.", nameof(destination));

        BuildLocalExtremes(source, sourceStride, sourceWidth, sourceHeight);
        BuildColumnMap(sourceWidth, outWidth);

        var work = new RowWork
        {
            Source = source,
            SourceStride = sourceStride,
            SourceHeight = sourceHeight,
            Destination = destination,
            DestinationStride = destinationStride,
            OutWidth = outWidth,
            ScaleY = (float)sourceHeight / outHeight,
            Steepness = (float)steepness,
            ExtremeStride = sourceWidth * BytesPerPixel,
        };

        // Rækkerne er uafhængige af hinanden og kan regnes samtidig. Men de deles op i nogle
        // få brede bånd i stedet for én opgave pr. række: hver eneste række er så lidt arbejde,
        // at det koster mere at fordele opgaverne end at udføre dem. Med bånd bruger luppen
        // en brøkdel af den processortid, den ellers ville.
        int bands = Math.Clamp(Environment.ProcessorCount, 1, Math.Max(1, outHeight / 24));

        if (bands <= 1)
        {
            for (int y = 0; y < outHeight; y++) RenderRow(y, work);
            return;
        }

        int rowsPerBand = (outHeight + bands - 1) / bands;
        Parallel.For(0, bands, band =>
        {
            int from = band * rowsPerBand;
            int to = Math.Min(from + rowsPerBand, outHeight);
            for (int y = from; y < to; y++) RenderRow(y, work);
        });
    }

    private sealed class RowWork
    {
        public required byte[] Source { get; init; }
        public required int SourceStride { get; init; }
        public required int SourceHeight { get; init; }
        public required byte[] Destination { get; init; }
        public required int DestinationStride { get; init; }
        public required int OutWidth { get; init; }
        public required float ScaleY { get; init; }
        public required float Steepness { get; init; }
        public required int ExtremeStride { get; init; }
    }

    private void RenderRow(int y, RowWork work)
    {
        float sourceY = (y + 0.5f) * work.ScaleY - 0.5f;
        int y0 = (int)MathF.Floor(sourceY);
        float weightY = sourceY - y0;
        float topWeight = 1 - weightY;

        int top = Math.Clamp(y0, 0, work.SourceHeight - 1);
        int bottom = Math.Clamp(y0 + 1, 0, work.SourceHeight - 1);
        int rowTop = top * work.SourceStride;
        int rowBottom = bottom * work.SourceStride;
        int extremeTop = top * work.ExtremeStride;
        int extremeBottom = bottom * work.ExtremeStride;

        var source = work.Source;
        var destination = work.Destination;
        int outOffset = y * work.DestinationStride;

        // Ved høj forstørrelse dækker det samme par kildepixels mange målpixels i træk.
        // Yderpunkterne findes derfor kun når parret skifter.
        int cachedColumn = -1;
        int dark = 0;
        int light = 0;
        int lumaSpan = 0;

        for (int x = 0; x < work.OutWidth; x++)
        {
            int left = _columnLeft[x];
            int right = _columnRight[x];

            if (left != cachedColumn)
            {
                cachedColumn = left;
                dark = Darkest(extremeTop + left, extremeTop + right, extremeBottom + left, extremeBottom + right);
                light = Lightest(extremeTop + left, extremeTop + right, extremeBottom + left, extremeBottom + right);
                lumaSpan = _maxima[light + 3] - _minima[dark + 3];
            }

            int a = rowTop + left;
            int b = rowTop + right;
            int c = rowBottom + left;
            int d = rowBottom + right;
            int target = outOffset + x * BytesPerPixel;

            // Er de fire nabopixels ens, er der intet at blande. Det gælder for det meste af
            // en skærmflade — baggrunden bag teksten — og det er dér langt de fleste pixels er.
            if (Same(source, a, b) && Same(source, a, c) && Same(source, a, d))
            {
                destination[target] = source[a];
                destination[target + 1] = source[a + 1];
                destination[target + 2] = source[a + 2];
                destination[target + 3] = 255;
                continue;
            }

            float weightX = _columnWeight[x];
            float w00 = (1 - weightX) * topWeight;
            float w10 = weightX * topWeight;
            float w01 = (1 - weightX) * weightY;
            float w11 = weightX * weightY;

            if (lumaSpan < FlatThreshold)
            {
                // Ingen kant her, kun en jævn overgang. Den blandes som den er.
                for (int channel = 0; channel < 3; channel++)
                {
                    destination[target + channel] = Clamp(
                        source[a + channel] * w00 + source[b + channel] * w10
                        + source[c + channel] * w01 + source[d + channel] * w11);
                }

                destination[target + 3] = 255;
                continue;
            }

            // Hvor langt henne på kanten pixlen ligger, afgøres af lysstyrken alene og gælder
            // alle tre farvekanaler under ét. Derfor blandes kun lysstyrken her — en tredjedel
            // af arbejdet — og den nye pixel lægges derefter mellem den mørkeste og den lyseste
            // farve der faktisk findes i omegnen. Aldrig en farve sat sammen af stumper fra
            // flere pixels, for den slags giver farveklatter langs bogstaverne.
            float luma = _luma[a >> 2] * w00 + _luma[b >> 2] * w10
                       + _luma[c >> 2] * w01 + _luma[d >> 2] * w11;

            float position = (luma - _minima[dark + 3]) / lumaSpan;
            float steepened = (position - 0.5f) * work.Steepness + 0.5f;

            if (steepened <= 0)
            {
                destination[target] = _minima[dark];
                destination[target + 1] = _minima[dark + 1];
                destination[target + 2] = _minima[dark + 2];
            }
            else if (steepened >= 1)
            {
                destination[target] = _maxima[light];
                destination[target + 1] = _maxima[light + 1];
                destination[target + 2] = _maxima[light + 2];
            }
            else
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    float low = _minima[dark + channel];
                    destination[target + channel] = Clamp(low + steepened * (_maxima[light + channel] - low));
                }
            }

            destination[target + 3] = 255;
        }
    }

    private static bool Same(byte[] pixels, int a, int b)
        => pixels[a] == pixels[b] && pixels[a + 1] == pixels[b + 1] && pixels[a + 2] == pixels[b + 2];

    /// <summary>Den af de fire naboers omegne der rummer den mørkeste farve.</summary>
    private int Darkest(int a, int b, int c, int d)
    {
        int best = a;
        if (_minima[b + 3] < _minima[best + 3]) best = b;
        if (_minima[c + 3] < _minima[best + 3]) best = c;
        if (_minima[d + 3] < _minima[best + 3]) best = d;
        return best;
    }

    /// <summary>Den af de fire naboers omegne der rummer den lyseste farve.</summary>
    private int Lightest(int a, int b, int c, int d)
    {
        int best = a;
        if (_maxima[b + 3] > _maxima[best + 3]) best = b;
        if (_maxima[c + 3] > _maxima[best + 3]) best = c;
        if (_maxima[d + 3] > _maxima[best + 3]) best = d;
        return best;
    }

    /// <summary>
    /// Finder for hver kildepixel den mørkeste og den lyseste farve i dens nærmeste omegn,
    /// og gemmer dem som hele farver sammen med deres lysstyrke.
    ///
    /// Det er nødvendigt fordi selve kantpixlen har en mellemtone. Måler man kun op mod de
    /// fire nærmeste naboer, bliver mellemtonen opfattet som "sin egen farve" og bredt ud til
    /// et synligt bånd langs bogstavet. Med omegnens yderpunkter som målestok bliver
    /// mellemtonen i stedet set som det den er: et sted midt på en kant.
    /// </summary>
    private void BuildLocalExtremes(byte[] source, int sourceStride, int width, int height)
    {
        int pixels = height * (sourceStride / BytesPerPixel);
        if (_luma.Length < pixels) _luma = new byte[pixels];

        for (int y = 0; y < height; y++)
        {
            int row = y * sourceStride;
            for (int x = 0; x < width; x++)
            {
                int offset = row + x * BytesPerPixel;
                _luma[offset >> 2] = (byte)((source[offset + 2] * 77 + source[offset + 1] * 150
                                           + source[offset] * 29) >> 8);
            }
        }

        int stride = width * BytesPerPixel;
        int needed = height * stride;

        if (_minima.Length < needed)
        {
            _minima = new byte[needed];
            _maxima = new byte[needed];
        }

        for (int y = 0; y < height; y++)
        {
            int top = Math.Max(y - 1, 0) * sourceStride;
            int middle = y * sourceStride;
            int bottom = Math.Min(y + 1, height - 1) * sourceStride;
            int outRow = y * stride;

            for (int x = 0; x < width; x++)
            {
                int left = Math.Max(x - 1, 0) * BytesPerPixel;
                int centre = x * BytesPerPixel;
                int right = Math.Min(x + 1, width - 1) * BytesPerPixel;

                int darkest = top + left;
                int lightest = darkest;

                Consider(top + centre, ref darkest, ref lightest);
                Consider(top + right, ref darkest, ref lightest);
                Consider(middle + left, ref darkest, ref lightest);
                Consider(middle + centre, ref darkest, ref lightest);
                Consider(middle + right, ref darkest, ref lightest);
                Consider(bottom + left, ref darkest, ref lightest);
                Consider(bottom + centre, ref darkest, ref lightest);
                Consider(bottom + right, ref darkest, ref lightest);

                int slot = outRow + centre;
                for (int channel = 0; channel < 3; channel++)
                {
                    _minima[slot + channel] = source[darkest + channel];
                    _maxima[slot + channel] = source[lightest + channel];
                }

                // Den fjerde plads i hver pixel er ubrugt og bruges her til lysstyrken.
                // Den er målestokken for hvor langt henne på kanten en ny pixel ligger.
                _minima[slot + 3] = _luma[darkest >> 2];
                _maxima[slot + 3] = _luma[lightest >> 2];
            }
        }
    }

    private void Consider(int offset, ref int darkest, ref int lightest)
    {
        byte luma = _luma[offset >> 2];
        if (luma < _luma[darkest >> 2]) darkest = offset;
        if (luma > _luma[lightest >> 2]) lightest = offset;
    }

    /// <summary>
    /// Hvilke to kildekolonner hver målkolonne ligger mellem. Det er ens for alle billedrækker,
    /// så det regnes ud én gang i stedet for en halv million gange.
    /// </summary>
    private void BuildColumnMap(int sourceWidth, int outWidth)
    {
        if (_columnLeft.Length < outWidth)
        {
            _columnLeft = new int[outWidth];
            _columnRight = new int[outWidth];
            _columnWeight = new float[outWidth];
        }

        float scaleX = (float)sourceWidth / outWidth;

        for (int x = 0; x < outWidth; x++)
        {
            float sourceX = (x + 0.5f) * scaleX - 0.5f;
            int x0 = (int)MathF.Floor(sourceX);

            _columnLeft[x] = Math.Clamp(x0, 0, sourceWidth - 1) * BytesPerPixel;
            _columnRight[x] = Math.Clamp(x0 + 1, 0, sourceWidth - 1) * BytesPerPixel;
            _columnWeight[x] = sourceX - x0;
        }
    }

    private static byte Clamp(float value)
        => value <= 0 ? (byte)0 : value >= 255 ? (byte)255 : (byte)(value + 0.5f);
}
