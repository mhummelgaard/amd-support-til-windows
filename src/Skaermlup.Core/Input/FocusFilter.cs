using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Input;

/// <summary>
/// Afgør om det felt, tastaturet står i, er til nogen hjælp for luppen — og hvor i feltet
/// luppen skal kigge.
/// </summary>
public static class FocusFilter
{
    /// <summary>
    /// Et felt der fylder over halvdelen af skærmen både på langs og på tværs, er et helt vindue
    /// eller et dokument — fx når man skifter program med Alt+Tab. Midten af det siger intet om,
    /// hvor brugeren er, så luppen bliver hvor den er. Tekst inde i et dokument følges i stedet
    /// af skrivemarkøren.
    /// </summary>
    public static PixelRect? Useful(PixelRect? focus, PixelRect monitor)
    {
        if (focus is not { IsEmpty: false } rect) return null;
        if (monitor.IsEmpty) return rect;

        bool wholeScreenish = rect.Width > monitor.Width / 2 && rect.Height > monitor.Height / 2;
        return wholeScreenish ? null : rect;
    }

    /// <summary>
    /// Punktet luppen skal stå på. Midten for almindelige felter. For lange, lave felter — en
    /// række i en liste over e-mails, et bredt søgefelt — ville midten ligge langt inde i
    /// rækken, mens teksten begynder til venstre. Så kigges der nær begyndelsen i stedet.
    /// </summary>
    public static PixelPoint Target(PixelRect focus)
    {
        int x = focus.X + Math.Min(focus.Width / 2, focus.Height * 4);
        return new PixelPoint(x, focus.Y + focus.Height / 2);
    }
}
