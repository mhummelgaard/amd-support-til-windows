using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Input;

/// <summary>
/// Holder fingerens tryk ude af musens plads.
///
/// Windows flytter musepilen hen, hvor fingeren rører skærmen. Lod luppen sig styre af det, ville
/// den springe hen til hvert eneste tryk på en knap eller et link uden for luppen — og et træk
/// for at rulle en side ville trække luppen med. På en berøringsskærm flyttes luppen i stedet
/// ved at trække i selve luppen, som et forstørrelsesglas. Kun en rigtig mus må flytte den ved
/// at pege.
///
/// Reglen: flyttede musepilen sig kort efter et tryk, var det fingeren, og så tæller det ikke.
/// </summary>
public sealed class PointerFilter
{
    private PixelPoint _raw;
    private PixelPoint _effective;
    private bool _seeded;

    /// <summary>Hvor længe efter et tryk en flytning af musepilen regnes for fingerens.</summary>
    public int TouchWindowMs { get; init; } = 300;

    /// <summary>Musens plads, som luppen skal se den.</summary>
    /// <param name="cursor">Hvor musepilen står nu.</param>
    /// <param name="nowMs">Nu, på samme ur som <paramref name="lastTouchMs"/>.</param>
    /// <param name="lastTouchMs">Hvornår fingeren sidst rørte skærmen. 0 hvis aldrig.</param>
    public PixelPoint Update(PixelPoint cursor, long nowMs, long lastTouchMs)
    {
        if (!_seeded)
        {
            _seeded = true;
            _raw = cursor;
            _effective = cursor;
            return _effective;
        }

        if (cursor == _raw) return _effective;
        _raw = cursor;

        bool byTouch = lastTouchMs > 0 && nowMs - lastTouchMs <= TouchWindowMs;
        if (!byTouch) _effective = cursor;
        return _effective;
    }
}
