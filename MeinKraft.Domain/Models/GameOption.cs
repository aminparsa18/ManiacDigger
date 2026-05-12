/// <summary>
/// Stores all user-configurable game settings, including graphics, audio,
/// input key bindings, and localisation preferences.
/// Initialised with sensible defaults in the constructor.
/// </summary>
public class GameOption
{
    public GameOption()
    {
        Shadows = false;
        Font = 0;
        DrawDistance = 1024;
        UseServerTextures = true;
        EnableSound = true;
        EnableAutoJump = false;
        ClientLanguage = "";
        Framerate = 0;
        Resolution = 0;
        Fullscreen = false;
        Smoothshadows = true;
        BlockShadowSave = 6f / 10;
        EnableBlockShadow = true;
        Keys = new int[360];
    }

    /// <summary>
    /// Whether to render dynamic shadows cast by the sun/moon.
    /// Defaults to <see langword="false"/> for performance.
    /// </summary>
    public bool Shadows { get; set; }

    /// <summary>
    /// Index of the active UI font. 0 = default font.
    /// </summary>
    public int Font { get; set; }

    /// <summary>
    /// Maximum view distance in blocks at which chunks are rendered.
    /// Defaults to 32. Higher values increase GPU and CPU load.
    /// </summary>
    public int DrawDistance { get; set; }

    /// <summary>
    /// When <see langword="true"/>, downloads and applies the texture pack
    /// provided by the connected server instead of the local one.
    /// </summary>
    public bool UseServerTextures { get; set; }

    /// <summary>
    /// When <see langword="true"/>, walk, break, place, and ambient sounds are played.
    /// </summary>
    public bool EnableSound { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the player automatically steps up one-block
    /// ledges without pressing jump.
    /// </summary>
    public bool EnableAutoJump { get; set; }

    /// <summary>
    /// BCP 47 language tag used for UI localisation (e.g. <c>"en"</c>, <c>"de"</c>).
    /// Empty string means use the system default.
    /// </summary>
    public string ClientLanguage { get; set; }

    /// <summary>
    /// Target frame-rate cap. 0 = uncapped / use monitor refresh rate.
    /// </summary>
    public int Framerate { get; set; }

    /// <summary>
    /// Index into the list of available screen resolutions. 0 = current desktop resolution.
    /// </summary>
    public int Resolution { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the game runs in exclusive fullscreen mode.
    /// </summary>
    public bool Fullscreen { get; set; }

    /// <summary>
    /// When <see langword="true"/>, shadow edges are blurred for a softer appearance.
    /// Has no effect when <see cref="Shadows"/> is <see langword="false"/>.
    /// </summary>
    public bool Smoothshadows { get; set; }

    /// <summary>
    /// Opacity of per-block ambient-occlusion shadows, in the range [0, 1].
    /// Defaults to 0.6. Lower values produce lighter shadows.
    /// </summary>
    public float BlockShadowSave { get; set; }

    /// <summary>
    /// When <see langword="true"/>, per-block ambient-occlusion shadows are rendered.
    /// </summary>
    public bool EnableBlockShadow { get; set; }

    /// <summary>
    /// Key binding table mapping action IDs to key codes.
    /// Sized to 360 entries to cover all possible action slots.
    /// </summary>
    public int[] Keys { get; set; }
}