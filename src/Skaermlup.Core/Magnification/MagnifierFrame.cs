using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Magnification;

/// <summary>
/// Ét billede fra luppen: hvilket skærmområde der læses, hvor det vises, hvor meget der
/// forstørres, og hvor fokuspunktet (musen eller tekstmarkøren) lander inde i visningen —
/// så markøren altid kan markeres det rigtige sted, også når luppen er klemt op mod en
/// skærmkant og kilde og visning ikke længere står præcis om samme punkt.
/// </summary>
public readonly record struct MagnifierFrame(
    PixelRect Source,
    PixelRect View,
    double ScaleX,
    double ScaleY,
    PixelPoint Marker,
    PixelPoint Focus)
{
    public bool IsEmpty => Source.IsEmpty || View.IsEmpty;

    /// <summary>
    /// Musens plads inde i lupvinduet, i skærmpixels fra vinduets øverste venstre hjørne.
    /// Det er dér der skal være hul igennem, så klik rammer programmet nedenunder.
    /// </summary>
    public PixelPoint HotSpot => new(Focus.X - View.X, Focus.Y - View.Y);
}
