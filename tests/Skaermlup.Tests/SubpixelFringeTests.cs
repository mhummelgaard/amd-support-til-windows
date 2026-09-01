using Shouldly;
using Skaermlup.Core.Magnification;

namespace Skaermlup.Tests;

public class SubpixelFringeTests
{
    private const int Bytes = SubpixelFringe.BytesPerPixel;

    private static byte[] Row(params (byte Blue, byte Green, byte Red)[] pixels)
    {
        var buffer = new byte[pixels.Length * Bytes];
        for (int i = 0; i < pixels.Length; i++)
        {
            buffer[i * Bytes] = pixels[i].Blue;
            buffer[i * Bytes + 1] = pixels[i].Green;
            buffer[i * Bytes + 2] = pixels[i].Red;
        }

        return buffer;
    }

    private static (byte Blue, byte Green, byte Red) At(byte[] buffer, int index)
        => (buffer[index * Bytes], buffer[index * Bytes + 1], buffer[index * Bytes + 2]);

    private static int Saturation((byte Blue, byte Green, byte Red) pixel)
        => Math.Max(Math.Max(pixel.Blue, pixel.Green), pixel.Red)
         - Math.Min(Math.Min(pixel.Blue, pixel.Green), pixel.Red);

    [Fact]
    public void Farvede_kanter_omkring_bogstaver_bliver_graa()
    {
        // Sådan ser en ClearType-kant ud: hvid, en rød og en blå bræmme, og så sort tekst.
        var row = Row((255, 255, 255), (120, 180, 255), (255, 180, 120), (0, 0, 0), (0, 0, 0));

        SubpixelFringe.Remove(row, row.Length, 5, 1);

        Saturation(At(row, 1)).ShouldBeLessThan(60);
        Saturation(At(row, 2)).ShouldBeLessThan(60);
    }

    [Fact]
    public void Lysstyrken_bliver_hvor_den_var()
    {
        // Kantens placering ligger i lysstyrken. Den må ikke flytte sig, ellers ville
        // bogstaverne blive tykkere eller tyndere af at fjerne farverne.
        var row = Row((255, 255, 255), (120, 180, 255), (255, 180, 120), (0, 0, 0));
        var before = new byte[row.Length];
        row.CopyTo(before, 0);

        SubpixelFringe.Remove(row, row.Length, 4, 1);

        for (int i = 0; i < 4; i++)
        {
            var (b1, g1, r1) = At(before, i);
            var (b2, g2, r2) = At(row, i);
            int lumaBefore = (r1 * 77 + g1 * 150 + b1 * 29) >> 8;
            int lumaAfter = (r2 * 77 + g2 * 150 + b2 * 29) >> 8;
            // Regnestykket holder lysstyrken helt fast; de få enheder er kun fordi en
            // farvekanal kan ramme loftet ved 255 og ikke kan komme længere op.
            lumaAfter.ShouldBeInRange(lumaBefore - 8, lumaBefore + 8);
        }
    }

    [Fact]
    public void En_rigtig_farvet_flade_beholder_sin_farve()
    {
        // En rød knap er rød over mange pixels i træk, og udjævningen må ikke tage farven.
        var row = Row((40, 40, 220), (40, 40, 220), (40, 40, 220), (40, 40, 220), (40, 40, 220));

        SubpixelFringe.Remove(row, row.Length, 5, 1);

        for (int i = 0; i < 5; i++)
        {
            var pixel = At(row, i);
            pixel.Red.ShouldBeGreaterThan((byte)190);
            pixel.Blue.ShouldBeLessThan((byte)70);
        }
    }

    [Fact]
    public void Graa_forbliver_helt_graa()
    {
        var row = Row((200, 200, 200), (128, 128, 128), (60, 60, 60), (0, 0, 0));

        SubpixelFringe.Remove(row, row.Length, 4, 1);

        for (int i = 0; i < 4; i++) Saturation(At(row, i)).ShouldBe(0);
    }

    [Fact]
    public void Meget_smalle_billeder_roeres_ikke()
    {
        var row = Row((10, 20, 30), (40, 50, 60));
        var before = (byte[])row.Clone();

        SubpixelFringe.Remove(row, row.Length, 2, 1);

        row.ShouldBe(before);
    }

    [Fact]
    public void En_for_lille_buffer_afvises()
    {
        var row = new byte[8];
        Should.Throw<ArgumentException>(() => SubpixelFringe.Remove(row, 5 * Bytes, 5, 1));
    }
}
