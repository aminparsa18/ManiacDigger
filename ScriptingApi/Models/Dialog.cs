using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class Dialog
{
    [ProtoMember(1, IsRequired = false)]
    public Widget[] Widgets;
    [ProtoMember(2, IsRequired = false)]
    public int Width;
    [ProtoMember(3, IsRequired = false)]
    public int Height;
    [ProtoMember(4, IsRequired = false)]
    public bool IsModal;
}
