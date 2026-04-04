using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class Widget
{
    [ProtoMember(1, IsRequired = false)]
    public string Id;
    [ProtoMember(2, IsRequired = false)]
    public bool Click;
    [ProtoMember(3, IsRequired = false)]
    public int X;
    [ProtoMember(4, IsRequired = false)]
    public int Y;
    [ProtoMember(5, IsRequired = false)]
    public int Width;
    [ProtoMember(6, IsRequired = false)]
    public int Height;
    [ProtoMember(7, IsRequired = false)]
    public string Text;
    [ProtoMember(8, IsRequired = false)]
    public char ClickKey;
    [ProtoMember(9, IsRequired = false)]
    public string Image;
    [ProtoMember(10, IsRequired = false)]
    public int Color = -1; //white
    [ProtoMember(11, IsRequired = false)]
    public DialogFont Font;
    [ProtoMember(12, IsRequired = false)]
    public WidgetType Type;

    public const string SolidImage = "Solid";

    public static Widget MakeSolid(float x, float y, float width, float height, int color)
    {
        Widget w = new()
        {
            Type = WidgetType.Image,
            Image = SolidImage,
            X = (int)x,
            Y = (int)y,
            Width = (int)width,
            Height = (int)height,
            Color = color
        };
        return w;
    }

    public static Widget MakeText(string text, DialogFont Font, float x, float y, int textColor)
    {
        Widget w = new()
        {
            Type = WidgetType.Text,
            Text = text,
            X = (int)x,
            Y = (int)y,
            Font = Font,
            Color = textColor
        };
        return w;
    }

    public static Widget MakeTextBox(string text, DialogFont Font, float x, float y, float width, float height, int textColor)
    {
        Widget w = new()
        {
            Type = WidgetType.TextBox,
            Text = text,
            X = (int)x,
            Y = (int)y,
            Width = (int)width,
            Height = (int)height,
            Font = Font,
            Color = textColor
        };
        return w;
    }
}