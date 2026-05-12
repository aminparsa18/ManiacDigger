public class MouseEventArgs
{
    public int X { get;  set; }
    public int Y { get; set; }

    public int MovementX { get; set; }
    public int MovementY { get; set; }

    public int Button { get; set; }
    public bool Handled { get; set; }
    public bool ForceUsage { get; set; }
    public bool Emulated { get; set; }
}