using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;

namespace MeinKraft;

/// <summary>
/// Represents a single audio playback task running on a dedicated thread-pool thread.
/// Supports play, pause, loop, spatial positioning, and clean shutdown.
/// </summary>
/// <remarks>
/// Call <see cref="Play"/> to start the thread. The thread exits naturally when
/// a non-looping clip finishes, when <see cref="Stop"/> is called, or when the
/// application signals <see cref="IGameExitService.Exit"/>.
/// </remarks>
/// <param name="gameExit">Application-exit signal; terminates the audio thread when raised.</param>
/// <param name="data">Decoded PCM data to play back.</param>
public sealed class AudioTask(IGameExitService gameExit, AudioData data)
{
    private readonly IGameExitService _gameExit = gameExit;
    private readonly AudioData _data = data;

    private volatile bool _started;
    private volatile bool _stopRequested;
    private volatile bool _shouldPlay;
    private volatile bool _restartRequested;
    private volatile bool _isFinished;

    private Vector3 _position;
    private readonly Lock _positionLock = new();

    /// <summary>
    /// World-space position used for OpenAL distance attenuation.
    /// Thread-safe; safe to update from the main thread during playback.
    /// </summary>
    public Vector3 Position
    {
        get
        {
            lock (_positionLock)
            {
                return _position;
            }
        }
        set
        {
            lock (_positionLock)
            {
                _position = value;
            }
        }
    }

    /// <summary>
    /// When <see langword="true"/>, the clip loops until <see cref="Stop"/> is called.
    /// Set this before the first <see cref="Play"/> call.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> once the audio thread has fully exited
    /// and all OpenAL resources have been released.
    /// </summary>
    public bool IsFinished => _isFinished;

    // ── Playback control ──────────────────────────────────────────────────────

    /// <summary>
    /// Starts playback on a thread-pool thread. If the task is already running,
    /// resumes from pause instead of spawning a second thread.
    /// </summary>
    public void Play()
    {
        _shouldPlay = true;
        if (_started)
        {
            return;
        }

        _started = true;
        ThreadPool.QueueUserWorkItem(_ => RunAudio());
    }

    /// <summary>Pauses playback without releasing resources. Call <see cref="Play"/> to resume.</summary>
    public void Pause() => _shouldPlay = false;

    /// <summary>
    /// Signals the audio thread to stop and release all OpenAL resources.
    /// Returns immediately; the thread exits asynchronously.
    /// </summary>
    public void Stop() => _stopRequested = true;

    /// <summary>
    /// Rewinds to the beginning of the clip on the next loop iteration.
    /// Has no effect on non-looping clips.
    /// </summary>
    public void Restart() => _restartRequested = true;

    // ── Audio thread ──────────────────────────────────────────────────────────

    private void RunAudio()
    {
        try
        {
            PlayInternal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioTask] Playback error: {ex}");
        }
        finally
        {
            _isFinished = true;
        }
    }

    private void PlayInternal()
    {
        int source = AL.GenSource();
        int buffer = AL.GenBuffer();

        try
        {
            AL.BufferData(buffer, AudioService.GetSoundFormat(_data.Channels, _data.BitsPerSample),
                _data.Pcm, _data.Rate);

            AL.DistanceModel(ALDistanceModel.InverseDistance);
            AL.Source(source, ALSourcef.RolloffFactor, 0.3f);
            AL.Source(source, ALSourcef.ReferenceDistance, 1f);
            AL.Source(source, ALSourcef.MaxDistance, 64f);
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.SourcePlay(source);

            while (!_stopRequested && !_gameExit.Exit)
            {
                AL.GetSource(source, ALGetSourcei.SourceState, out int rawState);
                ALSourceState state = (ALSourceState)rawState;

                if (!Loop && state != ALSourceState.Playing)
                {
                    break;
                }

                if (Loop)
                {
                    if (state == ALSourceState.Playing && !_shouldPlay)
                    {
                        AL.SourcePause(source);
                    }
                    else if (state != ALSourceState.Playing && _shouldPlay)
                    {
                        if (_restartRequested)
                        {
                            AL.SourceRewind(source);
                            _restartRequested = false;
                        }

                        AL.SourcePlay(source);
                    }
                }

                Vector3 pos = Position;
                AL.Source(source, ALSource3f.Position, pos.X, pos.Y, pos.Z);

                Thread.Sleep(1);
            }
        }
        finally
        {
            AL.SourceStop(source);
            AL.DeleteSource(source);
            AL.DeleteBuffer(buffer);
        }
    }
}