namespace ManicDigger;

public class DialogClickArgs
{
    internal int player;
    public int GetPlayer() { return player; }
    public void SetPlayer(int value) { player = value; }
    internal string widgetId;
    public string GetWidgetId() { return widgetId; }
    public void SetWidgetId(string value) { widgetId = value; }
    internal string[] textBoxValue;
    public string[] GetTextBoxValue() { return textBoxValue; }
    public void SetTextBoxValue(string[] value) { textBoxValue = value; }
}