using System.Text;

/// <summary>Renders multi-colored text into a single power-of-two <see cref="Bitmap"/>.</summary>
public class TextColorRenderer
{
    internal IGamePlatform platform;

    /// <summary>
    /// Renders a <see cref="TextStyle"/> value (which may contain inline color codes) into a
    /// <see cref="Bitmap"/> sized to the next power of two in each dimension.
    /// Each color segment is rendered separately and composited into a single atlas.
    /// </summary>
    /// <param name="t">The text and style parameters to render.</param>
    /// <returns>
    /// A <see cref="Bitmap"/> containing the rendered text, with transparent pixels where
    /// no glyph was drawn.
    /// </returns>
    internal Bitmap CreateTextTexture(TextStyle t)
    {
        TextPart[] parts = DecodeColors(t.Text, t.Color);

        float totalWidth = 0;
        float totalHeight = 0;
        int[] sizesX = new int[parts.Length];
        int[] sizesY = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            platform.TextSize(parts[i].text, t.FontSize, out int outWidth, out int outHeight);
            sizesX[i] = outWidth;
            sizesY[i] = outHeight;
            totalWidth += outWidth;
            totalHeight = Math.Max(totalHeight, outHeight);
        }

        int size2X = NextPowerOfTwo((int)totalWidth + 1);
        int size2Y = NextPowerOfTwo((int)totalHeight + 1);
        PixelBuffer atlas = PixelBuffer.Create(size2X, size2Y);

        float currentWidth = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            int sizeX = sizesX[i];
            int sizeY = sizesY[i];
            if (sizeX == 0 || sizeY == 0) continue;

            TextStyle partText = new()
            {
                Text = parts[i].text,
                Color = parts[i].color,
                FontSize = t.FontSize,
                FontStyle = t.FontStyle,
                FontFamily = t.FontFamily
            };

            PixelBuffer part = PixelBuffer.FromBitmap(platform.CreateTextTexture(partText));

            for (int y = 0; y < part.Height; y++)
            {
                for (int x = 0; x < part.Width; x++)
                {
                    if (x + currentWidth >= size2X || y >= size2Y) continue;

                    int c = part.GetPixel(x, y);
                    if (ColorUtils.ColorA(c) > 0)
                        atlas.SetPixel((int)currentWidth + x, y, c);
                }
            }

            currentWidth += sizeX;
        }

        return atlas.ToBitmap();
    }

    /// <summary>
    /// Splits <paramref name="s"/> into colored segments by parsing inline color codes of the
    /// form <c>&amp;X</c> where X is a hex digit (0–9, a–f). Unrecognised sequences are kept as-is.
    /// </summary>
    public static TextPart[] DecodeColors(string s, int defaultcolor)
    {
        List<TextPart> parts = [];
        int currentcolor = defaultcolor;
        StringBuilder currenttext = new();

        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&' && i + 1 < s.Length)
            {
                int color = HexToInt(s[i + 1]);
                if (color != -1)
                {
                    if (currenttext.Length > 0)
                    {
                        parts.Add(new TextPart { text = currenttext.ToString(), color = currentcolor });
                        currenttext.Clear();
                    }
                    currentcolor = GetColor(color);
                    i++; // skip the hex digit
                    continue;
                }
            }
            currenttext.Append(s[i]);
        }

        if (currenttext.Length > 0)
        {
            parts.Add(new TextPart { text = currenttext.ToString(), color = currentcolor });
        }

        return [.. parts];
    }

    /// <summary>
    /// Returns the smallest power of two that is greater than or equal to <paramref name="x"/>.
    /// Handles values up to 2^31.
    /// </summary>
    private static int NextPowerOfTwo(int x)
    {
        x--;
        x |= x >> 1;  // handle  2-bit numbers
        x |= x >> 2;  // handle  4-bit numbers
        x |= x >> 4;  // handle  8-bit numbers
        x |= x >> 8;  // handle 16-bit numbers
        x |= x >> 16; // handle 32-bit numbers
        x++;
        return x;
    }

    // ARGB color palette indexed by the hex digit in a color code sequence.
    // Loosely follows the classic 16-color terminal palette.
    private static readonly int[] ColorPalette =
    [
        ColorUtils.ColorFromArgb(255,   0,   0,   0), // 0 black
        ColorUtils.ColorFromArgb(255,   0,   0, 191), // 1 dark blue
        ColorUtils.ColorFromArgb(255,   0, 191,   0), // 2 dark green
        ColorUtils.ColorFromArgb(255,   0, 191, 191), // 3 dark cyan
        ColorUtils.ColorFromArgb(255, 191,   0,   0), // 4 dark red
        ColorUtils.ColorFromArgb(255, 191,   0, 191), // 5 dark magenta
        ColorUtils.ColorFromArgb(255, 191, 191,   0), // 6 dark yellow
        ColorUtils.ColorFromArgb(255, 191, 191, 191), // 7 light grey
        ColorUtils.ColorFromArgb(255,  40,  40,  40), // 8 dark grey
        ColorUtils.ColorFromArgb(255,  64,  64, 255), // 9 blue
        ColorUtils.ColorFromArgb(255,  64, 255,  64), // a green
        ColorUtils.ColorFromArgb(255,  64, 255, 255), // b cyan
        ColorUtils.ColorFromArgb(255, 255,  64,  64), // c red
        ColorUtils.ColorFromArgb(255, 255,  64, 255), // d magenta
        ColorUtils.ColorFromArgb(255, 255, 255,  64), // e yellow
        ColorUtils.ColorFromArgb(255, 255, 255, 255), // f white
    ];

    private static int GetColor(int index)
    {
        if (index >= 0 && index < ColorPalette.Length)
            return ColorPalette[index];
        return ColorPalette[15]; // default white
    }

    /// <summary>
    /// Converts a hex character (0–9, a–f, A–F) to its integer value, or -1 if not a hex digit.
    /// </summary>
    private static int HexToInt(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }
}
