using System.Numerics;

namespace MeinKraft;

/// <summary>
/// Represents a single audio playback task. Implementations are platform-specific
/// and created exclusively by <see cref="IAudioService.CreateAudio"/>.
/// </summary>
/// <remarks>
/// Core code only sees this abstract type — no platform SDK references leak
/// through. The concrete subclass lives in the platform project.
/// </remarks>
public abstract class AudioTask
{
    /// <summary>
    /// Returns <see langword="true"/> once the clip has finished playing or
    /// <see cref="Stop"/> has been called.
    /// </summary>
    public abstract bool IsFinished { get; }

    /// <summary>
    /// World-space position of the sound source used for distance attenuation.
    /// On platforms that do not support 3-D audio this property is accepted
    /// but has no audible effect.
    /// </summary>
    public abstract Vector3 Position { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the clip loops until <see cref="Stop"/>
    /// is called. Must be set before the first <see cref="Play"/> call.
    /// </summary>
    public abstract bool Loop { get; set; }

    /// <summary>Starts or resumes playback.</summary>
    public abstract void Play();

    /// <summary>Pauses playback without releasing resources.</summary>
    public abstract void Pause();

    /// <summary>
    /// Stops playback and releases all resources. The task cannot be reused
    /// after this call.
    /// </summary>
    public abstract void Stop();

    /// <summary>
    /// Rewinds to the beginning of the clip on the next loop iteration.
    /// Has no effect on non-looping clips.
    /// </summary>
    public abstract void Restart();
}