using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace ManicDigger;

/// <summary>
/// OpenAL-backed audio service. Owns the device and context for the application
/// lifetime. Lazily initialised — the device is created on the first operation.
/// </summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private readonly IGameExitService _gameExit;
    private readonly object _initLock = new();
    private ALDevice _device;
    private ALContext _context;
    private bool _initialised;
    private bool _disposed;

    /// <param name="gameExit">
    /// Application-exit signal forwarded to every <see cref="AudioTask"/> so their
    /// threads terminate cleanly on shutdown.
    /// </param>
    public AudioService(IGameExitService gameExit)
    {
        _gameExit = gameExit;
    }

    // ── IAudioService ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public AudioData CreateAudioData(byte[] data, int dataLength)
    {
        EnsureInitialised();
        using MemoryStream stream = new(data, 0, dataLength, writable: false);
        return IsRiffWave(stream) ? DecodeWave(stream) : OggDecoder.OggToWav(stream);
    }

    /// <inheritdoc/>
    public bool IsAudioDataLoaded(AudioData data) => data?.Pcm is { Length: > 0 };

    /// <inheritdoc/>
    public AudioTask CreateAudio(AudioData data)
    {
        EnsureInitialised();
        return new AudioTask(_gameExit, data);
    }

    /// <inheritdoc/>
    public void Play(AudioTask audio) => audio.Play();

    /// <inheritdoc/>
    public void Pause(AudioTask audio) => audio.Pause();

    /// <inheritdoc/>
    public void DestroyAudio(AudioTask audio) => audio.Stop();

    /// <inheritdoc/>
    public bool IsFinished(AudioTask audio) => audio.IsFinished;

    /// <inheritdoc/>
    public void SetPosition(AudioTask audio, float x, float y, float z)
        => audio.Position = new Vector3(x, y, z);

    /// <inheritdoc/>
    public void UpdateListener(
        float posX, float posY, float posZ,
        float orientX, float orientY, float orientZ)
    {
        EnsureInitialised();
        AL.Listener(ALListener3f.Position, posX, posY, posZ);
        Vector3 orientation = new(orientX, orientY, orientZ);
        Vector3 up = Vector3.UnitY;
        AL.Listener(ALListenerfv.Orientation, ref orientation, ref up);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>Destroys the OpenAL context and closes the audio device.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ALC.MakeContextCurrent(ALContext.Null);
        if (_context != ALContext.Null)
        {
            ALC.DestroyContext(_context);
        }

        if (_device != ALDevice.Null)
        {
            ALC.CloseDevice(_device);
        }
    }

    // ── Internal (used by AudioTask) ──────────────────────────────────────────

    internal static ALFormat GetSoundFormat(int channels, int bits) => (channels, bits) switch
    {
        (1, 8) => ALFormat.Mono8,
        (1, 16) => ALFormat.Mono16,
        (2, 8) => ALFormat.Stereo8,
        (2, 16) => ALFormat.Stereo16,
        _ => throw new NotSupportedException(
                       $"Unsupported audio format: {channels} ch / {bits} bit."),
    };

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EnsureInitialised()
    {
        if (_initialised)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialised)
            {
                return;
            }

            try
            {
                _device = ALC.OpenDevice(null);
                if (_device == ALDevice.Null)
                {
                    throw new InvalidOperationException("No audio device found.");
                }

                _context = ALC.CreateContext(_device, (int[])null);
                ALC.MakeContextCurrent(_context);
                _initialised = true;
            }
            catch (Exception ex)
            {
                TryLaunchOpenAlInstaller();
                Console.WriteLine(ex);
            }
        }
    }

    private static bool IsRiffWave(Stream stream)
    {
        bool riff = stream.ReadByte() == 'R'
                 && stream.ReadByte() == 'I'
                 && stream.ReadByte() == 'F'
                 && stream.ReadByte() == 'F';
        stream.Position = 0;
        return riff;
    }

    private static AudioData DecodeWave(Stream stream)
    {
        using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        Expect(new string(reader.ReadChars(4)), "RIFF", "Not a RIFF container.");
        reader.ReadInt32();
        Expect(new string(reader.ReadChars(4)), "WAVE", "Not a WAVE file.");
        Expect(new string(reader.ReadChars(4)), "fmt ", "Missing fmt chunk.");

        int fmtSize = reader.ReadInt32();
        reader.ReadInt16();
        int channels = reader.ReadInt16();
        int sampleRate = reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadInt16();
        int bitsPerSample = reader.ReadInt16();

        if (fmtSize > 16)
        {
            reader.ReadBytes(fmtSize - 16);
        }

        Expect(new string(reader.ReadChars(4)), "data", "Missing data chunk.");
        byte[] pcm = reader.ReadBytes(reader.ReadInt32());

        return new AudioData { Pcm = pcm, Channels = channels, Rate = sampleRate, BitsPerSample = bitsPerSample };
    }

    private static void Expect(string actual, string expected, string message)
    {
        if (actual != expected)
        {
            throw new NotSupportedException(message);
        }
    }

    private static void TryLaunchOpenAlInstaller()
    {
        const string installer = "oalinst.exe";
        if (!File.Exists(installer))
        {
            return;
        }

        try
        {
            Process.Start(installer, "/s");
        }
        catch { }
    }

    private const int SoundsMax = 64;

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
        for (int i = 0; i < SoundsCount; i++)
        {
            if (Sounds[i] is null)
            {
                Sounds[i] = sound;
                return;
            }
        }

        if (SoundsCount < SoundsMax)
        {
            Sounds[SoundsCount++] = sound;
        }
    }

    /// <inheritdoc/>
    public void StopAll()
    {
        for (int i = 0; i < SoundsCount; i++)
        {
            if (Sounds[i] is not null)
            {
                Sounds[i]!.Stop = true;
            }
        }
    }
}