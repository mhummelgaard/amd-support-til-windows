using System.IO;
using System.Runtime.InteropServices;
using Skaermlup.Core.Input;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// Skriver Windows' egne markører om — enten til ingenting, eller til den samme markør med en
/// kraftig ring om.
///
/// <b>Skjult.</b> Når luppen tegner sin egen markering, er systemets pil både overflødig og
/// misvisende: den tegnes på skærmens egen opløsning oven på det forstørrede billede, og når
/// luppen klemmes op mod en skærmkant, falder pilen og det den peger på ikke længere sammen.
/// Luppens egen markering regnes ud fra den faktiske afbildning og passer altid.
///
/// <b>Med ring.</b> Start-menuen og resten af Windows' egen skal ligger i et lag luppen ikke kan
/// nå. Der er kun to måder at få noget til at ligge oven på dem: uiAccess, som kræver at
/// programmet er kodesigneret — eller at tegne det ind i selve markøren, for den tegner Windows
/// øverst af alt. Det sidste er hvad der sker her, og det kræver ingenting af brugeren.
///
/// Der er ingen pæn måde at gøre nogen af delene på. Magnification-API'ets MagShowSystemCursor
/// svarer at det gik godt, men gør intet uden Windows' egen forstørrelsesmotor bagved, og
/// ShowCursor gælder kun ens eget vindue. Tilbage er at skrive systemets markører om — hvilket
/// gælder hele maskinen og bliver stående, hvis programmet ikke får ryddet op efter sig.
///
/// Derfor de tre sikkerhedsnet: markørerne røres kun mens luppen faktisk er tændt, de hentes
/// frem igen ad alle veje ud af programmet, og der lægges en lille fil på disken imens. Findes
/// den fil ved næste opstart, er programmet gået ned med markørerne skiftet ud, og så sættes de
/// på plads med det samme. Ingen må sidde tilbage uden en musepil.
/// </summary>
internal sealed class SystemCursor : IDisposable
{
    private const uint SPI_SETCURSORS = 0x0057;
    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;
    private const uint DI_NORMAL = 0x0003;

    /// <summary>Ringens kraftige farve. Gul ses på mørk bund, og den sorte kant bærer den på lys.</summary>
    private static readonly (byte B, byte G, byte R) RingCore = (0, 196, 255);

    private static readonly (byte B, byte G, byte R) RingEdge = (0, 0, 0);

    /// <summary>Alle de markører Windows selv skifter imellem alt efter hvad musen står på.</summary>
    private static readonly uint[] StandardCursors =
    [
        32512, // normal pil
        32513, // tekstmarkør
        32514, // timeglas
        32515, // kryds
        32516, // lodret pil
        32642, // størrelse, skråt
        32643, // størrelse, skråt den anden vej
        32644, // størrelse, vandret
        32645, // størrelse, lodret
        32646, // flyt
        32648, // forbudt
        32649, // hånd
        32650, // arbejder i baggrunden
        32651, // hjælp
    ];

    private enum Look
    {
        /// <summary>Brugerens egne markører, urørte.</summary>
        Untouched,
        Blank,
        Ring,
    }

    private Look _look = Look.Untouched;
    private CursorRingLayout _ring;

    /// <summary>
    /// Den størrelse Windows tegner markører i. Følger brugerens valg under Tilgængelighed →
    /// Musemarkør og berøring, så en bruger der allerede har skruet markøren op, får en
    /// tilsvarende større ring.
    /// </summary>
    public static int PointerSize
    {
        get
        {
            int width = GetSystemMetrics(SM_CXCURSOR);
            int height = GetSystemMetrics(SM_CYCURSOR);
            return Math.Min(width, height);
        }
    }

    private static string GuardFile => Path.Combine(
        Path.GetDirectoryName(SettingsStore.DefaultPath) ?? Path.GetTempPath(),
        "musepil-skjult");

    /// <summary>
    /// Sætter markørerne på plads igen, hvis programmet sidst gik ned mens de var skiftet ud.
    /// Kaldes ved opstart, før noget andet.
    /// </summary>
    public static void RestoreIfLeftHidden()
    {
        try
        {
            if (!File.Exists(GuardFile)) return;

            SystemParametersInfoW(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            File.Delete(GuardFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kan filen ikke læses, er der intet at rydde op efter.
        }
    }

    /// <summary>Falsk hvis Windows ikke ville skjule pilen. Så bliver den bare stående.</summary>
    public bool Hide()
    {
        if (_look == Look.Blank) return true;

        int size = PointerSize;
        if (size <= 0) return false;

        // Én maske der siger "lad skærmen stå" og én der ikke tegner noget: en markør der
        // fylder det den plejer, men ikke kan ses.
        //
        // Hver række i en enkeltbitsmaske fylder et helt antal ord. Regnes der bare size*size/8,
        // bliver bufferen for kort ved markørstørrelser der ikke går op i seksten, og så læser
        // Windows videre ud over den.
        int stride = (size + 15) / 16 * 2;
        var transparent = new byte[stride * size];
        var nothing = new byte[stride * size];
        Array.Fill(transparent, (byte)0xFF);

        return Replace(Look.Blank, _ =>
        {
            var blank = CreateCursor(IntPtr.Zero, 0, 0, size, size, transparent, nothing);
            return blank;
        });
    }

    /// <summary>
    /// Lægger en kraftig ring om markøren, uden at ændre hvilken markør der vises: pilen bliver
    /// en pil, tekstmarkøren en tekstmarkør. Falsk hvis Windows ikke ville, eller hvis
    /// markørerne ikke kunne tegnes — så bliver brugerens egne stående.
    /// </summary>
    public bool Highlight(CursorRingLayout ring)
    {
        if (ring.IsEmpty) return false;
        if (_look == Look.Ring && _ring == ring) return true;

        // Markørerne skal læses som brugeren har dem, ikke som vi sidst efterlod dem.
        if (_look != Look.Untouched) RestoreCursors();

        // Ringen er den samme på alle fjorten; kun selve markøren ovenpå er forskellig.
        byte[] backdrop = PaintRing(ring);

        bool ok = Replace(Look.Ring, id => BuildRingCursor(id, ring, backdrop));
        if (ok) _ring = ring;
        return ok;
    }

    /// <summary>
    /// Henter brugerens egne markører frem igen ved at få Windows til at indlæse dem forfra.
    /// Kaldes fra alle veje ud af programmet — også når noget går galt.
    /// </summary>
    public void Show()
    {
        if (_look == Look.Untouched) return;

        RestoreCursors();
        _look = Look.Untouched;
        _ring = CursorRingLayout.None;
        DeleteGuardFile();
    }

    public void Dispose() => Show();

    // ---------- Selve udskiftningen ----------

    /// <summary>
    /// Skifter alle fjorten markører ud med det <paramref name="build"/> laver til hver enkelt.
    /// Vagtfilen skrives før første udskiftning, så et nedbrud midt i stadig ryddes op.
    /// </summary>
    private bool Replace(Look look, Func<uint, IntPtr> build)
    {
        WriteGuardFile();

        bool any = false;
        foreach (uint id in StandardCursors)
        {
            var cursor = build(id);
            if (cursor == IntPtr.Zero) continue;

            // SetSystemCursor overtager markøren og frigiver den selv, så der skal laves
            // en ny til hver enkelt.
            if (SetSystemCursor(cursor, id)) any = true;
            else DestroyCursor(cursor);
        }

        if (!any)
        {
            // Kom vi halvvejs, står brugeren med noget halvt. Sæt det hele på plads igen.
            RestoreCursors();
            _look = Look.Untouched;
            DeleteGuardFile();
            return false;
        }

        _look = look;
        return true;
    }

    private static void RestoreCursors() => SystemParametersInfoW(SPI_SETCURSORS, 0, IntPtr.Zero, 0);

    // ---------- Ringen ----------

    /// <summary>
    /// Tegner ringen én gang: en gul kerne med sort kant i begge sider, så den kan ses både på
    /// en hvid side og på en mørk. Kanterne blødes ud over den yderste pixel, ellers får en rund
    /// ring trappetrin hele vejen rundt.
    ///
    /// Billedet er 32 bit med gennemsigtighed ganget ind i farven, som Windows forventer det.
    /// </summary>
    private static byte[] PaintRing(CursorRingLayout ring)
    {
        int size = ring.Canvas;
        var pixels = new byte[size * size * 4];

        double outer = ring.OuterRadius;
        double inner = ring.InnerRadius;
        double edge = Math.Max(2, ring.Thickness / 4.0);
        double centre = ring.HotSpot;

        for (int y = 0; y < size; y++)
        {
            double dy = y + 0.5 - centre;

            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - centre;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                // Hvor stor en del af pixlen ligger inden for ringen.
                double coverage = Math.Clamp(outer + 0.5 - distance, 0, 1)
                                * Math.Clamp(distance - inner + 0.5, 0, 1);
                if (coverage <= 0) continue;

                bool core = distance <= outer - edge && distance >= inner + edge;
                var (b, g, r) = core ? RingCore : RingEdge;

                int i = (y * size + x) * 4;
                pixels[i + 0] = (byte)Math.Round(b * coverage);
                pixels[i + 1] = (byte)Math.Round(g * coverage);
                pixels[i + 2] = (byte)Math.Round(r * coverage);
                pixels[i + 3] = (byte)Math.Round(255 * coverage);
            }
        }

        return pixels;
    }

    /// <summary>
    /// Bygger én markør: ringen med brugerens egen markør lagt oveni, og hotspot i midten så
    /// ringen ligger om det punkt der peges med.
    /// </summary>
    private static IntPtr BuildRingCursor(uint id, CursorRingLayout ring, byte[] backdrop)
    {
        var original = LoadCursorW(IntPtr.Zero, new IntPtr(id));
        if (original == IntPtr.Zero) return IntPtr.Zero;

        var pixels = (byte[])backdrop.Clone();
        if (!ComposeGlyph(original, ring, pixels)) return IntPtr.Zero;

        return CreateCursorFromPixels(pixels, ring);
    }

    /// <summary>
    /// Lægger den oprindelige markør oven på ringen.
    ///
    /// Gennemsigtigheden er det svære: GDI tegner ikke alfakanalen med over i et 32 bit billede,
    /// så den kan ikke bare aflæses bagefter. I stedet tegnes markøren to gange — én gang på
    /// sort og én gang på hvid. Forskellen mellem de to er præcis den baggrund der slap igennem,
    /// og dermed kendes gennemsigtigheden; billedet på sort <em>er</em> så farven med
    /// gennemsigtigheden ganget ind. Det virker for alle slags markører, også de gamle
    /// sort/hvide, hvor der ikke er nogen alfakanal at læse.
    /// </summary>
    private static bool ComposeGlyph(IntPtr cursor, CursorRingLayout ring, byte[] target)
    {
        if (!GetIconInfo(cursor, out var info)) return false;

        // GetIconInfo laver to bitmaps som vi selv skal rydde op efter.
        if (info.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(info.hbmMask);
        if (info.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(info.hbmColor);

        int x = ring.HotSpot - info.xHotspot;
        int y = ring.HotSpot - info.yHotspot;

        var onBlack = RenderCursor(cursor, ring.Canvas, x, y, fill: 0x00);
        var onWhite = RenderCursor(cursor, ring.Canvas, x, y, fill: 0xFF);
        if (onBlack is null || onWhite is null) return false;

        for (int i = 0; i < target.Length; i += 4)
        {
            // Den hvide baggrund der slap igennem, er det samme i alle tre farver.
            int alpha = Math.Clamp(255 - (onWhite[i + 1] - onBlack[i + 1]), 0, 255);
            if (alpha == 0) continue;

            int rest = 255 - alpha;
            for (int c = 0; c < 3; c++)
                target[i + c] = (byte)Math.Min(255, onBlack[i + c] + target[i + c] * rest / 255);

            target[i + 3] = (byte)Math.Min(255, alpha + target[i + 3] * rest / 255);
        }

        return true;
    }

    /// <summary>Tegner markøren på en ensfarvet baggrund og giver billedet tilbage.</summary>
    private static byte[]? RenderCursor(IntPtr cursor, int size, int x, int y, byte fill)
    {
        IntPtr dc = IntPtr.Zero, bitmap = IntPtr.Zero, previous = IntPtr.Zero;

        try
        {
            dc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;

            var header = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = size,
                // Negativ højde giver rækkerne oppefra og ned, ligesom resten af programmet.
                biHeight = -size,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            };

            bitmap = NativeMethods.CreateDIBSection(dc, ref header, NativeMethods.DIB_RGB_COLORS,
                out IntPtr bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) return null;

            previous = NativeMethods.SelectObject(dc, bitmap);

            var pixels = new byte[size * size * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = fill;
                pixels[i + 1] = fill;
                pixels[i + 2] = fill;
            }

            Marshal.Copy(pixels, 0, bits, pixels.Length);
            GdiFlush();

            if (!DrawIconEx(dc, x, y, cursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL)) return null;

            GdiFlush();
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            if (dc != IntPtr.Zero && previous != IntPtr.Zero) NativeMethods.SelectObject(dc, previous);
            if (bitmap != IntPtr.Zero) NativeMethods.DeleteObject(bitmap);
            if (dc != IntPtr.Zero) NativeMethods.DeleteDC(dc);
        }
    }

    /// <summary>Gør det færdige billede til en markør med hotspot i midten.</summary>
    private static IntPtr CreateCursorFromPixels(byte[] pixels, CursorRingLayout ring)
    {
        IntPtr colour = IntPtr.Zero, mask = IntPtr.Zero, dc = IntPtr.Zero;

        try
        {
            // CreateDIBSection vil gerne have et rigtigt håndtag at arbejde ud fra, også når
            // farverne kommer direkte fra billedet.
            dc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return IntPtr.Zero;

            var header = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = ring.Canvas,
                biHeight = -ring.Canvas,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            };

            colour = NativeMethods.CreateDIBSection(dc, ref header, NativeMethods.DIB_RGB_COLORS,
                out IntPtr bits, IntPtr.Zero, 0);
            if (colour == IntPtr.Zero || bits == IntPtr.Zero) return IntPtr.Zero;

            Marshal.Copy(pixels, 0, bits, pixels.Length);
            GdiFlush();

            // Med en alfakanal i farvebilledet skal masken bare være der. Nuller betyder
            // "brug farven som den er".
            mask = CreateBitmap(ring.Canvas, ring.Canvas, 1, 1, IntPtr.Zero);
            if (mask == IntPtr.Zero) return IntPtr.Zero;

            var info = new NativeMethods.ICONINFO
            {
                fIcon = 0,
                xHotspot = ring.HotSpot,
                yHotspot = ring.HotSpot,
                hbmMask = mask,
                hbmColor = colour,
            };

            return CreateIconIndirect(ref info);
        }
        finally
        {
            // CreateIconIndirect tager en kopi, så vores egne bitmaps skal væk igen.
            if (mask != IntPtr.Zero) NativeMethods.DeleteObject(mask);
            if (colour != IntPtr.Zero) NativeMethods.DeleteObject(colour);
            if (dc != IntPtr.Zero) NativeMethods.DeleteDC(dc);
        }
    }

    // ---------- Vagtfilen ----------

    private static void WriteGuardFile()
    {
        try
        {
            string? folder = Path.GetDirectoryName(GuardFile);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(GuardFile, "Markørerne er skiftet ud mens luppen kører.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Uden filen mister vi kun sikkerhedsnettet ved et nedbrud.
        }
    }

    private static void DeleteGuardFile()
    {
        try
        {
            if (File.Exists(GuardFile)) File.Delete(GuardFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ligegyldigt: næste opstart retter markørerne alligevel.
        }
    }

    // ---------- Windows ----------

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot,
        int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out NativeMethods.ICONINFO piconinfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref NativeMethods.ICONINFO piconinfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
        int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes,
        uint nBitCount, IntPtr lpBits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GdiFlush();
}
