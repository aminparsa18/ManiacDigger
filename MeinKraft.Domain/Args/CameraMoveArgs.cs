public class CameraMoveArgs
{
    public bool TurnLeft { get; }
    public bool TurnRight { get; }
    public bool DistanceUp { get; }
    public bool DistanceDown { get;}
    public bool AngleUp { get; set; }
    public bool AngleDown { get; set; }
    public float Distance { get; set; }
}