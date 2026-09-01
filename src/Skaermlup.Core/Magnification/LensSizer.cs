namespace Skaermlup.Core.Magnification;

/// <summary>Ændrer lupvinduets størrelse i procent af skærmen og holder facon undervejs.</summary>
public static class LensSizer
{
    public const int MinPercent = 12;
    public const int MaxPercent = 100;
    public const int StepPercent = 5;

    public static int ClampPercent(int percent) => Math.Clamp(percent, MinPercent, MaxPercent);

    /// <summary>
    /// Gør luppen større eller mindre. Forholdet mellem bredde og højde bevares, indtil den ene
    /// side rammer loftet — så holder begge sider op med at vokse i stedet for at blive skæve.
    /// </summary>
    public static (int Width, int Height) Resize(int widthPercent, int heightPercent, int deltaPercent)
    {
        int w = ClampPercent(widthPercent);
        int h = ClampPercent(heightPercent);
        if (deltaPercent == 0) return (w, h);

        double ratio = (double)h / w;
        int newWidth = ClampPercent(w + deltaPercent);

        int wanted = (int)Math.Round(newWidth * ratio);
        int newHeight = ClampPercent(wanted);
        if (newHeight != wanted && ratio > 0)
            newWidth = ClampPercent((int)Math.Round(newHeight / ratio));

        return (newWidth, newHeight);
    }
}
