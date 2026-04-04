using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class DialogFont
{
    public DialogFont()
    {
    }
    public DialogFont(string FamilyName, float Size, DialogFontStyle FontStyle)
    {
        this.FamilyName = FamilyName;
        this.Size = Size;
        this.FontStyle = FontStyle;
    }
    [ProtoMember(1, IsRequired = false)]
    public string FamilyName = "Verdana";
    [ProtoMember(2, IsRequired = false)]
    public float Size = 11f;
    [ProtoMember(3, IsRequired = false)]
    public DialogFontStyle FontStyle;
}