using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Groups all sound variant arrays for a single block or item type.
/// Each array holds one or more asset filenames; the engine picks one at random
/// on playback to add variety. Empty arrays mean no sound is played.
/// </summary>
[MemoryPackable]
public partial class SoundSet
{
    /// <summary>Sounds played when the player walks on this block.</summary>
    public string[] Walk { get; set; } = [];

    /// <summary>Sounds played when this block is broken/mined.</summary>
    public string[] Break { get; set; } = [];

    /// <summary>Sounds played when this block is placed.</summary>
    public string[] Build { get; set; } = [];

    /// <summary>Sounds played when this block is cloned (middle-click pick).</summary>
    public string[] Clone { get; set; } = [];

    /// <summary>Sounds played when this weapon is fired.</summary>
    public string[] Shoot { get; set; } = [];

    /// <summary>Sounds played when this weapon stops firing (trigger released).</summary>
    public string[] ShootEnd { get; set; } = [];

    /// <summary>Sounds played when this weapon is reloaded.</summary>
    public string[] Reload { get; set; } = [];
}