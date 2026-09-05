using Shouldly;
using Skaermlup.Core.Input;

namespace Skaermlup.Tests;

public class CursorRingTests
{
    /// <summary>Den markørstørrelse Windows melder ved standardindstillingen.</summary>
    private const int NormalPointer = 32;

    [Fact]
    public void Standardstoerrelsen_giver_en_ring_der_kan_ses()
    {
        var ring = CursorRing.Compute(NormalPointer, CursorRing.DefaultScalePercent);

        ring.IsEmpty.ShouldBeFalse();
        ring.Canvas.ShouldBe(96);
        ring.HotSpot.ShouldBe(48);
        ring.Thickness.ShouldBeGreaterThanOrEqualTo(CursorRing.MinThickness);
    }

    /// <summary>
    /// Pilen tegnes med sit hotspot i midten og fylder derfra sin egen størrelse ud. Ringen skal
    /// ligge uden for den, ellers skærer den tværs gennem pilen.
    /// </summary>
    [Theory]
    [InlineData(32, 250)]
    [InlineData(32, 300)]
    [InlineData(32, 400)]
    [InlineData(48, 300)]
    [InlineData(64, 400)]
    [InlineData(96, 300)]
    public void Ringen_ligger_altid_uden_for_pilen(int pointer, int scale)
    {
        var ring = CursorRing.Compute(pointer, scale);

        ring.IsEmpty.ShouldBeFalse();
        ring.InnerRadius.ShouldBeGreaterThanOrEqualTo(pointer);
    }

    [Theory]
    [InlineData(32, 250)]
    [InlineData(32, 300)]
    [InlineData(64, 400)]
    [InlineData(96, 300)]
    public void Ringen_ligger_altid_inde_i_billedet(int pointer, int scale)
    {
        var ring = CursorRing.Compute(pointer, scale);

        ring.OuterRadius.ShouldBeLessThan(ring.Canvas / 2);
        (ring.Canvas % 2).ShouldBe(0);
        ring.HotSpot.ShouldBe(ring.Canvas / 2);
    }

    /// <summary>
    /// Er der mindre plads, skal ringen blive tyndere frem for at give op. Først når selv den
    /// tyndeste ring ikke kan være der, meldes der ingen ring.
    /// </summary>
    [Fact]
    public void Ringen_bliver_tyndere_naar_der_er_mindre_plads()
    {
        var stor = CursorRing.Compute(NormalPointer, 300);
        var lille = CursorRing.Compute(NormalPointer, 250);

        lille.IsEmpty.ShouldBeFalse();
        lille.Thickness.ShouldBeLessThan(stor.Thickness);
    }

    [Fact]
    public void For_lidt_plads_giver_ingen_ring_i_stedet_for_en_der_ser_forkert_ud()
        => CursorRing.Compute(NormalPointer, 200).IsEmpty.ShouldBeTrue();

    /// <summary>
    /// Windows tegner ikke markører større end 256x256. Ved en meget stor markørstørrelse
    /// rammer man loftet, og så er der ikke plads uden om pilen.
    /// </summary>
    [Fact]
    public void En_meget_stor_markoer_giver_ingen_ring()
    {
        var ring = CursorRing.Compute(128, 300);

        ring.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Billedet_bliver_aldrig_stoerre_end_Windows_kan_tegne()
        => CursorRing.Compute(96, CursorRing.MaxScalePercent).Canvas.ShouldBeLessThanOrEqualTo(CursorRing.MaxCanvas);

    [Fact]
    public void En_ubrugelig_markoerstoerrelse_giver_ingen_ring()
    {
        CursorRing.Compute(0, 300).IsEmpty.ShouldBeTrue();
        CursorRing.Compute(-4, 300).IsEmpty.ShouldBeTrue();
        CursorRing.Compute(CursorRing.MinPointerSize - 1, 300).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Skalering_uden_for_omraadet_klemmes_paa_plads()
    {
        CursorRing.ClampScale(10).ShouldBe(CursorRing.MinScalePercent);
        CursorRing.ClampScale(9000).ShouldBe(CursorRing.MaxScalePercent);
        CursorRing.ClampScale(CursorRing.DefaultScalePercent).ShouldBe(CursorRing.DefaultScalePercent);
    }

    [Fact]
    public void En_ring_der_ikke_findes_er_tom()
    {
        CursorRingLayout.None.IsEmpty.ShouldBeTrue();
        CursorRingLayout.None.ShouldBe(default(CursorRingLayout));
    }
}
