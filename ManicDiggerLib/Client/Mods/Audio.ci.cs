//This manages all in-game audio. On each frame it checks if assets are fully loaded,
//then walks a pool of up to 64 active sounds and handles each one through four stages:
//load & play(if the audio data is ready), update its 3D world position, stop it if flagged, 
//and either restart it (looping sounds) or clear it (one-shot sounds) when it finishes.

using static ManicDigger.AudioOpenAl;

/// <summary>
/// Mod that manages audio loading, playback, positional updates, looping, and cleanup.
/// </summary>
public class ModAudio : ModBase
{
    private readonly Dictionary<string, AudioData> audioData = new();
    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    private bool wasLoaded;

    public ModAudio(IGameClient game, IGamePlatform gamePlatform)
    {
        this.game = game;
        this.platform = gamePlatform;
    }

    public override void OnNewFrame(float args)
    {
        if (game.AssetsLoadProgress != 1)
            return;

        if (!wasLoaded)
        {
            wasLoaded = true;
            Preload();
        }

        ProcessSounds();
    }

    private void ProcessSounds()
    {
        for (int i = 0; i < game.Audio.soundsCount; i++)
        {
            Sound sound = game.Audio.sounds[i];
            if (sound == null) continue;

            TryLoad(i, sound);
            TryUpdatePosition(sound);
            TryStop(i, sound);
            TryLoopOrFinish(i, sound);
        }
    }

    /// <summary>Attempts to create and play audio for a sound that hasn't been loaded yet.</summary>
    private void TryLoad(int i, Sound sound)
    {
        if (sound.audio != null) return;

        AudioData data = GetAudioData(sound.name);
        if (platform.AudioDataLoaded(data))
        {
            sound.audio = platform.AudioCreate(data);
            platform.AudioPlay(sound.audio);
        }
    }

    /// <summary>Updates the 3D position of an active sound source.</summary>
    private void TryUpdatePosition(Sound sound)
    {
        if (sound.audio == null) return;
        platform.AudioSetPosition(sound.audio, sound.x, sound.y, sound.z);
    }

    /// <summary>Deletes and nulls out any sound marked for stopping.</summary>
    private void TryStop(int i, Sound sound)
    {
        if (sound.audio == null || !sound.stop) return;
        platform.AudioDelete(sound.audio);
        game.Audio.sounds[i] = null;
    }

    /// <summary>Loops finished looping sounds or clears finished one-shot sounds.</summary>
    private void TryLoopOrFinish(int i, Sound sound)
    {
        if (sound.audio == null) return;
        if (!platform.AudioFinished(sound.audio)) return;

        if (sound.loop)
        {
            AudioData data = GetAudioData(sound.name);
            if (platform.AudioDataLoaded(data))
            {
                sound.audio = platform.AudioCreate(data);
                platform.AudioPlay(sound.audio);
            }
        }
        else
        {
            game.Audio.sounds[i] = null;
        }
    }

    /// <summary>Preloads all .ogg assets found in the asset list.</summary>
    private void Preload()
    {
        for (int k = 0; k < game.Assets.Count; k++)
        {
            string name = game.Assets[k].name;
            if (!name.EndsWith(".ogg")) 
                continue;
            GetAudioData(name);
        }
    }

    /// <summary>Returns cached audio data for the given sound name, loading it if necessary.</summary>
    private AudioData GetAudioData(string sound)
    {
        if (!audioData.TryGetValue(sound, out AudioData data))
        {
            data = platform.AudioDataCreate(game.GetAssetFile(sound), game.GetAssetFileLength(sound));
            audioData[sound] = data;
        }
        return data;
    }
}

/// <summary>
/// Manages a fixed-size pool of active sounds.
/// </summary>
public class AudioControl
{
    private const int SoundsMax = 64;

    internal readonly Sound[] sounds = new Sound[SoundsMax];
    internal int soundsCount;

    /// <summary>Removes all active sounds from the pool.</summary>
    public void Clear()
    {
        Array.Clear(sounds, 0, soundsCount);
        soundsCount = 0;
    }

    /// <summary>Adds a sound to the pool, reusing a null slot if available.</summary>
    public void Add(Sound s)
    {
        for (int i = 0; i < soundsCount; i++)
        {
            if (sounds[i] == null)
            {
                sounds[i] = s;
                return;
            }
        }
        if (soundsCount < SoundsMax)
            sounds[soundsCount++] = s;
    }

    /// <summary>Marks all active sounds for stopping on the next frame.</summary>
    public void StopAll()
    {
        for (int i = 0; i < soundsCount; i++)
        {
            sounds[i]?.stop = true;
        }
    }
}

/// <summary>
/// Represents an active or pending sound instance in the world.
/// </summary>
public class Sound
{
    internal string name;
    internal float x;
    internal float y;
    internal float z;
    internal bool loop;
    internal bool stop;
    internal AudioTask audio;
}