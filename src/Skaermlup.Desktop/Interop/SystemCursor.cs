using System.IO;
using System.Runtime.InteropServices;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// Skjuler og viser Windows' egen musepil.
///
/// Når luppen tegner sin egen markering, er systemets pil både overflødig og misvisende: den
/// tegnes på skærmens egen opløsning oven på det forstørrede billede, og når luppen klemmes op
/// mod en skærmkant, falder pilen og det den peger på ikke længere sammen. Luppens egen
/// markering regnes ud fra den faktiske afbildning og passer altid.
///
/// Der er ingen pæn måde at gøre det på. Magnification-API'ets MagShowSystemCursor svarer at
/// det gik godt, men gør intet uden Windows' egen forstørrelsesmotor bagved, og ShowCursor
/// gælder kun ens eget vindue. Tilbage er at skrive systemets markører om — hvilket gælder
/// hele maskinen og bliver stående, hvis programmet ikke får ryddet op efter sig.
///
/// Derfor de tre sikkerhedsnet: pilen skjules kun mens luppen faktisk er tændt, den hentes
/// frem igen ad alle veje ud af programmet, og der lægges en lille fil på disken imens. Findes
/// den fil ved næste opstart, er programmet gået ned med pilen skjult, og så sættes markørerne
/// på plads med det samme. Ingen må sidde tilbage uden en musepil.
/// </summary>
internal sealed class SystemCursor : IDisposable
{
    private const uint SPI_SETCURSORS = 0x0057;
    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;

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

    private bool _hidden;

    private static string GuardFile => Path.Combine(
        Path.GetDirectoryName(SettingsStore.DefaultPath) ?? Path.GetTempPath(),
        "musepil-skjult");

    /// <summary>
    /// Sætter markørerne på plads igen, hvis programmet sidst gik ned mens pilen var skjult.
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
        if (_hidden) return true;

        int width = GetSystemMetrics(SM_CXCURSOR);
        int height = GetSystemMetrics(SM_CYCURSOR);
        if (width <= 0 || height <= 0) return false;

        // Én maske der siger "lad skærmen stå" og én der ikke tegner noget: en markør der
        // fylder det den plejer, men ikke kan ses.
        var transparent = new byte[width * height / 8];
        var nothing = new byte[width * height / 8];
        Array.Fill(transparent, (byte)0xFF);

        WriteGuardFile();

        bool any = false;
        foreach (uint id in StandardCursors)
        {
            var blank = CreateCursor(IntPtr.Zero, 0, 0, width, height, transparent, nothing);
            if (blank == IntPtr.Zero) continue;

            // SetSystemCursor overtager markøren og frigiver den selv, så der skal laves
            // en ny til hver enkelt.
            if (SetSystemCursor(blank, id)) any = true;
            else DestroyCursor(blank);
        }

        if (!any)
        {
            DeleteGuardFile();
            return false;
        }

        _hidden = true;
        return true;
    }

    /// <summary>
    /// Henter pilen frem igen ved at få Windows til at indlæse brugerens egne markører forfra.
    /// Kaldes fra alle veje ud af programmet — også når noget går galt.
    /// </summary>
    public void Show()
    {
        if (!_hidden) return;

        SystemParametersInfoW(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        _hidden = false;
        DeleteGuardFile();
    }

    private static void WriteGuardFile()
    {
        try
        {
            string? folder = Path.GetDirectoryName(GuardFile);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(GuardFile, "Musepilen er skjult mens luppen kører.");
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

    public void Dispose() => Show();

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
}
