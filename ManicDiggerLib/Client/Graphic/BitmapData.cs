using System.Drawing.Imaging;
using System.Runtime.InteropServices;

/// <summary>
/// A platform-independent ARGB pixel buffer that can be read from and written to
/// <see cref="Bitmap"/> objects using either a fast (Windows) or safe (Linux) path.
/// </summary>
public class PixelBuffer
{
    /// <summary>Gets the width of the buffer in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the height of the buffer in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>The raw ARGB pixel data in row-major order.</summary>
    public int[] Argb { get; private set; }

    private static readonly bool IsMono = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>Creates an empty <see cref="PixelBuffer"/> of the given dimensions.</summary>
    public static PixelBuffer Create(int width, int height) => new()
    {
        Width = width,
        Height = height,
        Argb = new int[width * height]
    };

    /// <summary>Creates a <see cref="PixelBuffer"/> populated with pixels from <paramref name="bitmap"/>.</summary>
    public static PixelBuffer FromBitmap(Bitmap bitmap)
    {
        var buffer = Create(bitmap.Width, bitmap.Height);
        ReadFromBitmap(bitmap, buffer.Argb);
        return buffer;
    }

    /// <summary>Sets the ARGB color of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void SetPixel(int x, int y, int color) => Argb[x + y * Width] = color;

    /// <summary>Gets the ARGB color of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public int GetPixel(int x, int y) => Argb[x + y * Width];

    /// <summary>Renders this buffer into a new <see cref="Bitmap"/>.</summary>
    public Bitmap ToBitmap()
    {
        Bitmap bmp = new(Width, Height);
        WriteToBitmap(bmp, Argb);
        return bmp;
    }

    // ── Bitmap I/O ────────────────────────────────────────────────────────

    private static void ReadFromBitmap(Bitmap bitmap, int[] pixels)
    {
        if (IsMono) ReadSafe(bitmap, pixels);
        else ReadFast(bitmap, pixels);
    }

    private static void WriteToBitmap(Bitmap bmp, int[] pixels)
    {
        if (IsMono) WriteSafe(bmp, pixels);
        else WriteFast(bmp, pixels);
    }

    /// <summary>Slow but portable pixel read via <see cref="Bitmap.GetPixel"/>.</summary>
    private static void ReadSafe(Bitmap bmp, int[] pixels)
    {
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                pixels[y * bmp.Width + x] = bmp.GetPixel(x, y).ToArgb();
    }

    /// <summary>
    /// Fast pixel read via <see cref="BitmapData"/> and <see cref="Marshal.Copy"/>.
    /// Converts to <see cref="PixelFormat.Format32bppArgb"/> first if needed.
    /// </summary>
    private static void ReadFast(Bitmap bmp, int[] pixels)
    {
        Bitmap source = bmp.PixelFormat == PixelFormat.Format32bppArgb
            ? bmp
            : new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);

        if (!ReferenceEquals(source, bmp))
        {
            using Graphics g = Graphics.FromImage(source);
            g.DrawImage(bmp, 0, 0);
        }

        BitmapData bmd = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(bmd.Scan0, pixels, 0, source.Width * source.Height);
        }
        finally
        {
            source.UnlockBits(bmd);
            if (!ReferenceEquals(source, bmp)) source.Dispose();
        }
    }

    /// <summary>Slow but portable pixel write via <see cref="Bitmap.SetPixel"/>.</summary>
    private static void WriteSafe(Bitmap bmp, int[] pixels)
    {
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                bmp.SetPixel(x, y, Color.FromArgb(pixels[y * bmp.Width + x]));
    }

    /// <summary>Fast pixel write via <see cref="BitmapData"/> and <see cref="Marshal.Copy"/>.</summary>
    private static void WriteFast(Bitmap bmp, int[] pixels)
    {
        BitmapData bmd = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(pixels, 0, bmd.Scan0, bmp.Width * bmp.Height);
        }
        finally
        {
            bmp.UnlockBits(bmd);
        }
    }
}