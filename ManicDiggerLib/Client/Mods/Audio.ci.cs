/// <summary>
/// Mod that manages audio loading, playback, positional updates, looping, and cleanup.
/// </summary>
public class ModAudio : ModBase
{
    private readonly Dictionary<string, AudioData> audioData = new();
    private bool wasLoaded;

    public override void OnNewFrame(Game game, NewFrameEventArgs args)
    {
        if (game.assetsLoadProgress != 1)
            return;

        if (!wasLoaded)
        {
            wasLoaded = true;
            Preload(game);
        }

        ProcessSounds(game);
    }

    private void ProcessSounds(Game game)
    {
        for (int i = 0; i < game.audio.soundsCount; i++)
        {
            Sound sound = game.audio.sounds[i];
            if (sound == null) continue;

            TryLoad(game, i, sound);
            TryUpdatePosition(game, sound);
            TryStop(game, i, sound);
            TryLoopOrFinish(game, i, sound);
        }
    }

    /// <summary>Attempts to create and play audio for a sound that hasn't been loaded yet.</summary>
    private void TryLoad(Game game, int i, Sound sound)
    {
        if (sound.audio != null) return;

        AudioData data = GetAudioData(game, sound.name);
        if (game.platform.AudioDataLoaded(data))
        {
            sound.audio = game.platform.AudioCreate(data);
            game.platform.AudioPlay(sound.audio);
        }
    }

    /// <summary>Updates the 3D position of an active sound source.</summary>
    private static void TryUpdatePosition(Game game, Sound sound)
    {
        if (sound.audio == null) return;
        game.platform.AudioSetPosition(sound.audio, sound.x, sound.y, sound.z);
    }

    /// <summary>Deletes and nulls out any sound marked for stopping.</summary>
    private static void TryStop(Game game, int i, Sound sound)
    {
        if (sound.audio == null || !sound.stop) return;
        game.platform.AudioDelete(sound.audio);
        game.audio.sounds[i] = null;
    }

    /// <summary>Loops finished looping sounds or clears finished one-shot sounds.</summary>
    private void TryLoopOrFinish(Game game, int i, Sound sound)
    {
        if (sound.audio == null) return;
        if (!game.platform.AudioFinished(sound.audio)) return;

        if (sound.loop)
        {
            AudioData data = GetAudioData(game, sound.name);
            if (game.platform.AudioDataLoaded(data))
            {
                sound.audio = game.platform.AudioCreate(data);
                game.platform.AudioPlay(sound.audio);
            }
        }
        else
        {
            game.audio.sounds[i] = null;
        }
    }

    /// <summary>Preloads all .ogg assets found in the asset list.</summary>
    private void Preload(Game game)
    {
        for (int k = 0; k < game.assets.count; k++)
        {
            string name = game.assets.items[k].name;
            if (!name.EndsWith(".ogg")) 
                continue;
            GetAudioData(game, name);
        }
    }

    /// <summary>Returns cached audio data for the given sound name, loading it if necessary.</summary>
    private AudioData GetAudioData(Game game, string sound)
    {
        if (!audioData.TryGetValue(sound, out AudioData data))
        {
            data = game.platform.AudioDataCreate(game.GetAssetFile(sound), game.GetAssetFileLength(sound));
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
    internal AudioCi audio;
}