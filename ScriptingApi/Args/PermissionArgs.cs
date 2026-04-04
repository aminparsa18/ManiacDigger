namespace ManicDigger;

public class PermissionArgs
{
    internal int player;
    internal int x;
    internal int y;
    internal int z;
    public int GetPlayer() { return player; }
    public void SetPlayer(int value) { player = value; }
    public int GetX() { return x; }
    public void SetX(int value) { x = value; }
    public int GetY() { return y; }
    public void SetY(int value) { y = value; }
    public int GetZ() { return z; }
    public void SetZ(int value) { z = value; }

    internal bool allowed;
    public bool GetAllowed() { return allowed; }
    public void SetAllowed(bool value) { allowed = value; }
}