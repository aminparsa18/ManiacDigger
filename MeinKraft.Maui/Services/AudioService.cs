using Plugin.Maui.Audio;
using System.Numerics;

namespace MeinKraft;

/// <summary>
/// Plugin.Maui.Audio-backed audio service. Works on Android, iOS, Windows, and
/// macOS without requiring OpenAL or any native runtime installation.
/// </summary>
/// <remarks>
/// <para>
/// <b>3-D positional audio</b> — Plugin.Maui.Audio has no spatial audio API.
/// <see cref="SetPosition"/> and <see cref="UpdateListener"/> are accepted for
/// interface compatibility but have no audible effect.
/// </para>
/// <para>
/// <b>DI registration</b> — register once at app startup:
/// <code>builder.AddAudio();</code>
/// Then inject <see cref="IAudioManager"/> into this class, or pass
/// <c>AudioManager.Current</c> when DI is unavailable.
/// </para>
/// </remarks>
public sealed class AudioService : IAudioService, IDisposable
{
    private readonly IAudioManager _audioManager;
    private bool _disposed;

    private const int SoundsMax = 64;

    /// <param name="audioManager">
    /// Injected via DI (<c>builder.AddAudio()</c>) or <c>AudioManager.Current</c>.
    /// Lifetime is owned by the DI container — this class does not dispose it.
    /// </param>
    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    // ── IAudioService ─────────────────────────────────────────────────────────

    /// <summary>
    /// Copies <paramref name="dataLength"/> bytes from <paramref name="data"/>
    /// into an <see cref="AudioData"/> blob. The copy is retained for the
    /// lifetime of the <see cref="AudioData"/> so the caller can free the
    /// original buffer immediately.
    /// </summary>
    public AudioData CreateAudioData(byte[] data, int dataLength)
    {
        byte[] copy = new byte[dataLength];
        Buffer.BlockCopy(data, 0, copy, 0, dataLength);
        return new AudioData { RawBytes = copy };
    }

    /// <inheritdoc/>
    public bool IsAudioDataLoaded(AudioData data) => data?.RawBytes is { Length: > 0 };

    /// <summary>
    /// Creates a new <see cref="AudioTask"/> backed by a fresh
    /// <see cref="IAudioPlayer"/>. Multiple tasks can share the same
    /// <see cref="AudioData"/> without interfering with each other.
    /// </summary>
    public AudioTask CreateAudio(AudioData data)
    {
        // A new MemoryStream per play: some platform backends require the
        // stream to start at position 0 and do not support seeking.
        MemoryStream stream = new(data.RawBytes!, writable: false);
        IAudioPlayer player = _audioManager.CreatePlayer(stream);
        return new MauiAudioTask(player, stream);
    }

    /// <inheritdoc/>
    public void Play(AudioTask audio) => audio.Play();

    /// <inheritdoc/>
    public void Pause(AudioTask audio) => audio.Pause();

    /// <inheritdoc/>
    public void DestroyAudio(AudioTask audio) => audio.Stop();

    /// <inheritdoc/>
    public bool IsFinished(AudioTask audio) => audio.IsFinished;

    /// <summary>
    /// No-op — Plugin.Maui.Audio does not support 3-D positional audio.
    /// Accepted for interface compatibility with the OpenAL backend.
    /// </summary>
    public void SetPosition(AudioTask audio, float x, float y, float z) { }

    /// <summary>
    /// No-op — Plugin.Maui.Audio does not support listener orientation.
    /// Accepted for interface compatibility with the OpenAL backend.
    /// </summary>
    public void UpdateListener(
        float posX, float posY, float posZ,
        float orientX, float orientY, float orientZ)
    { }

    // ── Sound pool ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Sound?[] Sounds { get; } = new Sound[SoundsMax];

    /// <inheritdoc/>
    public int SoundsCount { get; private set; }

    /// <inheritdoc/>
    public void Clear()
    {
        Array.Clear(Sounds, 0, SoundsCount);
        SoundsCount = 0;
    }

    /// <inheritdoc/>
    public void Add(Sound sound)
    {
        // Prefer a previously freed slot over appending to the tail.
        for (int i = 0; i < SoundsCount; i++)
        {
            if (Sounds[i] is null)
            {
                Sounds[i] = sound;
                return;
            }
        }

        if (SoundsCount < SoundsMax)
            Sounds[SoundsCount++] = sound;
    }

    /// <inheritdoc/>
    public void StopAll()
    {
        for (int i = 0; i < SoundsCount; i++)
        {
            if (Sounds[i] is not null)
                Sounds[i]!.Stop = true;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>
    /// Signals all active sounds to stop. The <see cref="IAudioManager"/>
    /// lifetime is owned by DI and is not disposed here.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAll();
    }

    // ── MauiAudioTask (private — Plugin.Maui.Audio never leaks to core) ───────

    /// <summary>
    /// Concrete <see cref="AudioTask"/> that wraps an <see cref="IAudioPlayer"/>.
    /// Kept private to <see cref="AudioService"/> so no Plugin.Maui.Audio type
    /// is ever visible to the core project.
    /// </summary>
    private sealed class MauiAudioTask : AudioTask, IDisposable
    {
        private readonly IAudioPlayer _player;
        private readonly MemoryStream _stream;
        private bool _isFinished;
        private bool _disposed;
        private Vector3 _position;
        private bool _loop;

        internal MauiAudioTask(IAudioPlayer player, MemoryStream stream)
        {
            _player = player;
            _stream = stream;
            _player.PlaybackEnded += OnPlaybackEnded;
        }

        public override bool IsFinished => _isFinished;

        /// <summary>
        /// Stored for interface compatibility; has no audible effect because
        /// Plugin.Maui.Audio does not expose a spatial audio API.
        /// </summary>
        public override Vector3 Position
        {
            get => _position;
            set => _position = value;
        }

        public override bool Loop
        {
            get => _loop;
            set
            {
                _loop = value;
                _player.Loop = value;
            }
        }

        public override void Play()
        {
            if (!_disposed)
                _player.Play();
        }

        public override void Pause()
        {
            if (!_disposed)
                _player.Pause();
        }

        public override void Stop()
        {
            if (!_disposed)
                _player.Stop();
            Dispose();
        }

        public override void Restart()
        {
            if (_disposed)
                return;
            _player.Stop();
            _player.Play();
        }

        private void OnPlaybackEnded(object? sender, EventArgs e) => _isFinished = true;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isFinished = true;
            _player.PlaybackEnded -= OnPlaybackEnded;
            _player.Dispose();
            _stream.Dispose();
        }
    }
}