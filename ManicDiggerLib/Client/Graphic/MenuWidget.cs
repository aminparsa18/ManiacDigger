public class MenuWidget
{
    public MenuWidget()
    {
        visible = true;
        fontSize = 14;
        nextWidget = -1;
        hasKeyboardFocus = false;
    }

    public void GetFocus()
    {
        hasKeyboardFocus = true;
        if (type == WidgetType.Textbox)
        {
            editing = true;
        }
    }

    public void LoseFocus()
    {
        hasKeyboardFocus = false;
        if (type == WidgetType.Textbox)
        {
            editing = false;
        }
    }

    internal string text;
    internal float x;
    internal float y;
    internal float sizex;
    internal float sizey;
    internal bool pressed;
    internal bool hover;
    internal WidgetType type;
    internal bool editing;
    internal bool visible;
    internal float fontSize;
    internal string description;
    internal bool password;
    internal bool selected;
    internal ButtonStyle buttonStyle;
    internal string image;
    internal int nextWidget;
    internal bool hasKeyboardFocus;
    internal int color;
    internal string id;
    internal bool isbutton;
    internal Font font;
}