namespace MeinKraft;

// ─────────────────────────────────────────────────────────────────────────────
// AudioData
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Decoded (or raw) audio file bytes ready to be handed to the audio service.
/// The same <see cref="AudioData"/> instance can be played multiple times
/// concurrently — a fresh <see cref="MemoryStream"/> is created per play.
/// </summary>
public sealed class AudioData
{
    /// <summary>
    /// Raw audio file bytes (WAV or OGG).  The platform decoder inside
    /// Plugin.Maui.Audio handles format detection and decoding at play time.
    /// </summary>
    public byte[]? RawBytes { get; init; }
}
