using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class SoundSet
{
    [ProtoMember(1)]
    public string[] Walk = [];
    [ProtoMember(2)]
    public string[] Break = [];
    [ProtoMember(3)]
    public string[] Build = [];
    [ProtoMember(4)]
    public string[] Clone = [];
    [ProtoMember(5)]
    public string[] Shoot = [];
    [ProtoMember(6)]
    public string[] ShootEnd = [];
    [ProtoMember(7)]
    public string[] Reload = [];
}