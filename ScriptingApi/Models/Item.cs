using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class Item
{
    [ProtoMember(1, IsRequired = false)]
    public ItemClass ItemClass;
    [ProtoMember(2, IsRequired = false)]
    public string ItemId;
    [ProtoMember(3, IsRequired = false)]
    public int BlockId;
    [ProtoMember(4, IsRequired = false)]
    public int BlockCount = 1;
}