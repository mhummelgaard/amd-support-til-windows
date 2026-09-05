namespace Skaermlup.Core.Input;

/// <summary>Målene på ringmarkøren, i pixels på det billede der bliver til selve markøren.</summary>
/// <param name="Canvas">Markørbilledets bredde og højde. Altid et lige tal.</param>
/// <param name="HotSpot">Punktet i billedet der peger — midten, så ringen ligger om musen.</param>
/// <param name="OuterRadius">Ringens yderkant målt fra hotspot.</param>
/// <param name="Thickness">Ringens bredde.</param>
public readonly record struct CursorRingLayout(int Canvas, int HotSpot, int OuterRadius, int Thickness)
{
    /// <summary>Ingen ring — der var ikke plads.</summary>
    public static CursorRingLayout None => default;

    public bool IsEmpty => Canvas <= 0;

    /// <summary>Ringens inderkant. Pilen skal kunne være indenfor.</summary>
    public int InnerRadius => OuterRadius - Thickness;
}

/// <summary>
/// Regner ud hvor stor ringen om musen kan være.
///
/// Ringen tegnes ind i selve markørbilledet, og Windows tegner markører i den størrelse
/// brugeren har valgt under Tilgængelighed → Musemarkør og berøring. Den størrelse melder
/// systemet som <c>SM_CXCURSOR</c>, og den er også den plads pilen selv fylder ud fra sit
/// hotspot. Ringen skal altså ligge <em>uden for</em> pilen i et billede der er flere gange
/// så stort som pilen — ellers skærer ringen tværs gennem den.
///
/// Windows tegner ikke markører større end 256x256, og det loft rammer man ved store
/// markørstørrelser. Derfor gøres ringen først tyndere, og kan den stadig ikke være der,
/// meldes der ingen ring tilbage — så bliver den almindelige musepil stående i stedet for
/// at blive erstattet af noget der ser forkert ud.
/// </summary>
public static class CursorRing
{
    /// <summary>Melder systemet en mindre markør end det, er svaret ikke til at regne med.</summary>
    public const int MinPointerSize = 12;

    /// <summary>Windows tegner ikke markører større end dette.</summary>
    public const int MaxCanvas = 256;

    public const int MinThickness = 3;

    public const int MaxThickness = 20;

    public const int MinScalePercent = 200;

    public const int MaxScalePercent = 400;

    /// <summary>Tre gange pilens egen størrelse. Ringen er tydelig og dækker stadig lidt.</summary>
    public const int DefaultScalePercent = 300;

    public static int ClampScale(int percent) => Math.Clamp(percent, MinScalePercent, MaxScalePercent);

    /// <summary>
    /// Målene på ringen ved den markørstørrelse systemet melder. <see cref="CursorRingLayout.None"/>
    /// hvis der ikke er plads til en ring der kan ses uden at ligge oven i pilen.
    /// </summary>
    public static CursorRingLayout Compute(int pointerSize, int scalePercent)
    {
        if (pointerSize < MinPointerSize) return CursorRingLayout.None;

        int canvas = (int)Math.Round(pointerSize * ClampScale(scalePercent) / 100.0);
        canvas = Math.Min(canvas, MaxCanvas);

        // Et lige tal, så hotspot ligger præcis i midten og ringen bliver rund.
        canvas -= canvas % 2;
        if (canvas <= 0) return CursorRingLayout.None;

        int outer = canvas / 2 - 1;

        // Pilen tegnes med sit hotspot i midten og fylder derfra sin egen størrelse ud.
        // Det der er tilbage ude ved kanten, er alt hvad ringen har at være i.
        int room = outer - pointerSize;
        if (room < MinThickness) return CursorRingLayout.None;

        int thickness = Math.Clamp((int)Math.Round(canvas * 0.11), MinThickness, Math.Min(MaxThickness, room));
        return new CursorRingLayout(canvas, canvas / 2, outer, thickness);
    }
}
