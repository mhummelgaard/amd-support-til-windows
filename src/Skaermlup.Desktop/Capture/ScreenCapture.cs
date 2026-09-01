using Skaermlup.Core.Geometry;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Capture;

/// <summary>
/// Henter et udsnit af skærmen. Den samme buffer genbruges mellem billeder, så der ikke
/// bliver allokeret hukommelse tres gange i sekundet.
/// </summary>
internal sealed unsafe class ScreenCapture : IDisposable
{
    private IntPtr _screenDc;
    private IntPtr _memoryDc;
    private IntPtr _bitmap;
    private IntPtr _previousBitmap;
    private byte* _bits;
    private bool _disposed;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int Stride => Width * 4;

    public ReadOnlySpan<byte> Pixels =>
        _bits is null || Width <= 0 || Height <= 0
            ? ReadOnlySpan<byte>.Empty
            : new ReadOnlySpan<byte>(_bits, Height * Stride);

    /// <summary>Kopierer skærmudsnittet ned i bufferen. Falsk hvis Windows ikke kunne levere billedet.</summary>
    public bool Capture(PixelRect source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (source.IsEmpty) return false;

        if (!EnsureScreenDc()) return false;
        if (!EnsureBuffer(source.Width, source.Height)) return false;

        if (NativeMethods.BitBlt(_memoryDc, 0, 0, source.Width, source.Height,
                _screenDc, source.X, source.Y, NativeMethods.SRCCOPY))
        {
            return true;
        }

        // Skærmopsætningen kan have ændret sig (ny opløsning, skærm slået fra).
        // Smid håndtaget væk og prøv igen ved næste billede.
        ReleaseScreenDc();
        return false;
    }

    private bool EnsureScreenDc()
    {
        if (_screenDc != IntPtr.Zero) return true;

        _screenDc = NativeMethods.CreateDCW("DISPLAY", null, null, IntPtr.Zero);
        return _screenDc != IntPtr.Zero;
    }

    private bool EnsureBuffer(int width, int height)
    {
        if (_bitmap != IntPtr.Zero && width == Width && height == Height) return true;

        ReleaseBuffer();

        var header = new NativeMethods.BITMAPINFOHEADER
        {
            biSize = sizeof(NativeMethods.BITMAPINFOHEADER),
            biWidth = width,
            // Negativ højde giver rækkerne oppefra og ned, ligesom WPF forventer dem.
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = NativeMethods.BI_RGB,
        };

        _memoryDc = NativeMethods.CreateCompatibleDC(_screenDc);
        if (_memoryDc == IntPtr.Zero) return false;

        _bitmap = NativeMethods.CreateDIBSection(_memoryDc, ref header, NativeMethods.DIB_RGB_COLORS,
            out IntPtr bits, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero || bits == IntPtr.Zero)
        {
            ReleaseBuffer();
            return false;
        }

        _previousBitmap = NativeMethods.SelectObject(_memoryDc, _bitmap);
        _bits = (byte*)bits;
        Width = width;
        Height = height;
        return true;
    }

    private void ReleaseBuffer()
    {
        if (_memoryDc != IntPtr.Zero && _previousBitmap != IntPtr.Zero)
            NativeMethods.SelectObject(_memoryDc, _previousBitmap);

        if (_bitmap != IntPtr.Zero) NativeMethods.DeleteObject(_bitmap);
        if (_memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(_memoryDc);

        _previousBitmap = IntPtr.Zero;
        _bitmap = IntPtr.Zero;
        _memoryDc = IntPtr.Zero;
        _bits = null;
        Width = 0;
        Height = 0;
    }

    private void ReleaseScreenDc()
    {
        if (_screenDc != IntPtr.Zero) NativeMethods.DeleteDC(_screenDc);
        _screenDc = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseBuffer();
        ReleaseScreenDc();
    }
}
