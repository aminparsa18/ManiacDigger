
/// <summary>Registry of all monster types available in the game.</summary>
public class GameDataMonsters
{
    /// <summary>Defines the code, display name, and skin asset for a single monster type.</summary>
    public record MonsterData(string Code, string Name, string Skin);

    /// <summary>All registered monster types, in definition order.</summary>
    public IReadOnlyList<MonsterData> Monsters { get; } =
    [
        new("imp.txt",     "Imp",      "imp.png"),
        new("imp.txt",     "Fire Imp", "impfire.png"),
        new("dragon.txt",  "Dragon",   "dragon.png"),
        new("zombie.txt",  "Zombie",   "zombie.png"),
        new("cyclops.txt", "Cyclops",  "cyclops.png"),
    ];
}
