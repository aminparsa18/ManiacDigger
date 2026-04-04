using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class ProtoPoint
{
    [ProtoMember(1, IsRequired = false)]
    public int X;
    [ProtoMember(2, IsRequired = false)]
    public int Y;
    public ProtoPoint()
    {
    }
    public ProtoPoint(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    public override bool Equals(object? obj)
    {
        if (obj is ProtoPoint obj2)
        {
            return this.X == obj2.X
                && this.Y == obj2.Y;
        }
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return X ^ Y;
    }
}