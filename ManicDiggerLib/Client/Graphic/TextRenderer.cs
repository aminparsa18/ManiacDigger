using System.Drawing.Drawing2D;

public class TextRenderer
{
    public static FontType Font = FontType.Nice;

    public static void SetFont(int fontID)
    {
        Font = (FontType)fontID;
    }

    private static Bitmap DefaultFont(TextStyle t)
    {
        //outlined font looks smaller
        t.FontSize = Math.Max(t.FontSize, 9);
        t.FontSize *= 1.65f;
        Font font = new("Arial", t.FontSize, t.FontStyle);

        SizeF size;
        using (Bitmap bmp = new(1, 1))
        {
            using Graphics g = Graphics.FromImage(bmp);
            size = g.MeasureString(t.Text, font, new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
        }
        size.Width *= 0.7f;

        SizeF size2 = new(NextPowerOfTwo((uint)size.Width), NextPowerOfTwo((uint)size.Height));
        if (size2.Width == 0 || size2.Height == 0)
        {
            return new Bitmap(1, 1);
        }
        Bitmap bmp2 = new((int)size2.Width, (int)size2.Height);
        using (Graphics g2 = Graphics.FromImage(bmp2))
        {
            if (size.Width != 0 && size.Height != 0)
            {
                StringFormat format = StringFormat.GenericTypographic;

                g2.FillRectangle(new SolidBrush(Color.FromArgb(textalpha, 0, 0, 0)), 0, 0, size.Width, size.Height);
                g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Rectangle rect = new() { X = 0, Y = 0 };
                using GraphicsPath path = GetStringPath(t.Text, t.FontSize, rect, font, format);
                g2.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF off = rect;
                off.Offset(2, 2);
                using (GraphicsPath offPath = GetStringPath(t.Text, t.FontSize, off, font, format))
                {
                    Brush b = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
                    g2.FillPath(b, offPath);
                    b.Dispose();
                }
                g2.FillPath(new SolidBrush(Color.FromArgb(t.Color)), path);
                g2.DrawPath(Pens.Black, path);
            }
        }
        return bmp2;
    }

    private static Bitmap BlackBackgroundFont(TextStyle t)
    {
        Font font = new("Verdana", t.FontSize, t.FontStyle);
        SizeF size;
        using (Bitmap bmp = new(1, 1))
        {
            using Graphics g = Graphics.FromImage(bmp);
            size = g.MeasureString(t.Text, font, new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
        }

        SizeF size2 = new(NextPowerOfTwo((uint)size.Width), NextPowerOfTwo((uint)size.Height));
        if (size2.Width == 0 || size2.Height == 0)
        {
            return new Bitmap(1, 1);
        }
        Bitmap bmp2 = new((int)size2.Width, (int)size2.Height);
        using (Graphics g2 = Graphics.FromImage(bmp2))
        {
            if (size.Width != 0 && size.Height != 0)
            {
                g2.FillRectangle(new SolidBrush(Color.Black), 0, 0, size.Width, size.Height);
                g2.DrawString(t.Text, font, new SolidBrush(Color.FromArgb(t.Color)), 0, 0);
            }
        }
        return bmp2;
    }

    private static Bitmap SimpleFont(TextStyle t)
    {
        Font font = new("Arial", (float)t.FontSize, t.FontStyle);

        SizeF size;
        using (Bitmap bmp = new(1, 1))
        {
            using Graphics g = Graphics.FromImage(bmp);
            size = g.MeasureString(t.Text, font, new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
        }

        SizeF size2 = new(NextPowerOfTwo((uint)size.Width), NextPowerOfTwo((uint)size.Height));
        if (size2.Width == 0 || size2.Height == 0)
        {
            return new Bitmap(1, 1);
        }
        Bitmap bmp2 = new((int)size2.Width, (int)size2.Height);

        using (Graphics g2 = Graphics.FromImage(bmp2))
        {
            if (size.Width != 0 && size.Height != 0)
            {
                g2.SmoothingMode = SmoothingMode.AntiAlias;
                g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g2.DrawString(t.Text, font, new SolidBrush(Color.FromArgb(t.Color)), 0, 0);
            }
        }
        return bmp2;
    }

    private static Bitmap NiceFont(TextStyle t)
    {
        float fontsize = t.FontSize;
        Font font;
        fontsize = Math.Max(fontsize, 9);
        fontsize *= 1.1f;
        font = new Font(t.FontFamily, fontsize, t.FontStyle);

        SizeF size;
        using (Bitmap bmp = new(1, 1))
        {
            using Graphics g = Graphics.FromImage(bmp);
            size = g.MeasureString(t.Text, font, new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
        }

        SizeF size2 = new(NextPowerOfTwo((uint)size.Width), NextPowerOfTwo((uint)size.Height));
        if (size2.Width == 0 || size2.Height == 0)
        {
            return new Bitmap(1, 1);
        }
        Bitmap bmp2 = new((int)size2.Width, (int)size2.Height);
        using (Graphics g2 = Graphics.FromImage(bmp2))
        {
            if (size.Width != 0 && size.Height != 0)
            {
                g2.SmoothingMode = SmoothingMode.AntiAlias;
                g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                Matrix mx = new(1f, 0, 0, 1f, 1, 1);
                g2.Transform = mx;
                g2.DrawString(t.Text, font, new SolidBrush(Color.FromArgb(128, Color.Black)), 0, 0);
                g2.ResetTransform();

                g2.DrawString(t.Text, font, new SolidBrush(Color.FromArgb(t.Color)), 0, 0);
            }
        }
        return bmp2;
    }

    public static Bitmap MakeTextTexture(TextStyle t)
    {
        return Font switch
        {
            FontType.Default => DefaultFont(t),
            FontType.BlackBackground => BlackBackgroundFont(t),
            FontType.Simple => SimpleFont(t),
            FontType.Nice => NiceFont(t),
            _ => DefaultFont(t),
        };
    }

    private static GraphicsPath GetStringPath(string s, float emSize, RectangleF rect, Font font, StringFormat format)
    {
        GraphicsPath path = new();
        // TODO: Bug in Mono. Returns incomplete list of points / cuts string.
        path.AddString(s, font.FontFamily, (int)font.Style, emSize, rect, format);
        return path;
    }

    private static readonly int textalpha = 0;
    protected static uint NextPowerOfTwo(uint x)
    {
        x--;
        x |= x >> 1;  // handle  2 bit numbers
        x |= x >> 2;  // handle  4 bit numbers
        x |= x >> 4;  // handle  8 bit numbers
        x |= x >> 8;  // handle 16 bit numbers
        x |= x >> 16; // handle 32 bit numbers
        x++;
        return x;
    }

    protected static int HexToInt(char c)
    {
        if (c == '0') { return 0; }
        if (c == '1') { return 1; }
        if (c == '2') { return 2; }
        if (c == '3') { return 3; }
        if (c == '4') { return 4; }
        if (c == '5') { return 5; }
        if (c == '6') { return 6; }
        if (c == '7') { return 7; }
        if (c == '8') { return 8; }
        if (c == '9') { return 9; }
        if (c == 'a') { return 10; }
        if (c == 'b') { return 11; }
        if (c == 'c') { return 12; }
        if (c == 'd') { return 13; }
        if (c == 'e') { return 14; }
        if (c == 'f') { return 15; }
        return -1;
    }

    private static SizeF MeasureTextSize(string text, float fontsize)
    {
        string text2 = "";
        fontsize = Math.Max(fontsize, 9);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&')
            {
                if (i + 1 < text.Length && HexToInt(text[i + 1]) != -1)
                {
                    //Skip color codes when calculating text length
                    i++;
                }
                else
                {
                    text2 += text[i];
                }
            }
            else
            {
                text2 += text[i];
            }
        }
        using Font font = new("Verdana", fontsize);
        using Bitmap bmp = new(1, 1);
        using Graphics g = Graphics.FromImage(bmp);
        return g.MeasureString(text2, font, new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
    }

    private static readonly Dictionary<TextStyle, SizeF> textsizes = [];
    private const int TextSizeCacheMax = 512;
    private static SizeF TextSize(string text, float fontsize)
    {
        TextStyle key = new TextStyle() { Text = text, FontSize = fontsize };

        if (textsizes.TryGetValue(key, out SizeF size))
            return size;

        size = MeasureTextSize(text, fontsize);

        if (textsizes.Count >= TextSizeCacheMax)
            textsizes.Clear();

        textsizes[key] = size;
        return size;
    }

    public static void TextSize(string text, float fontSize, out int outWidth, out int outHeight)
    {
        SizeF size = TextSize(text, fontSize);
        outWidth = (int)size.Width;
        outHeight = (int)size.Height;
    }
}

public enum FontType
{
    Nice,
    Simple,
    BlackBackground,
    Default
}