public partial class Game
{
    // -------------------------------------------------------------------------
    // One-shot sounds
    // -------------------------------------------------------------------------

    public void PlayAudio(string file)
    {
        if (!AudioEnabled)
            return;

        PlayAudioAt(file, EyesPosX(), EyesPosY(), EyesPosZ());
    }

    public void PlayAudio(string name, float x, float y, float z)
    {
        if (x == 0 && y == 0 && z == 0)
            PlayAudio(name);
        else
            PlayAudioAt(name, x, z, y);
    }

    public void PlayAudioAt(string file, float x, float y, float z)
    {
        if (file == null || !AudioEnabled || assetsLoadProgress != 1)
            return;

        string file_ = file.Replace(".wav", ".ogg");
        if (GetAssetFileLength(file_) == 0)
        {
            platform.ConsoleWriteLine(string.Format("File not found: {0}", file));
            return;
        }

        audio.Add(new Sound { name = file_, x = x, y = y, z = z });
    }

    // -------------------------------------------------------------------------
    // Looping sounds
    // -------------------------------------------------------------------------

    public void AudioPlayLoop(string file, bool play, bool restart)
    {
        if (!AudioEnabled && play)
            return;

        if (assetsLoadProgress != 1)
            return;

        string file_ = file.Replace(".wav", ".ogg");
        if (GetAssetFileLength(file_) == 0)
        {
            platform.ConsoleWriteLine(string.Format("File not found: {0}", file));
            return;
        }

        if (play)
        {
            Sound s = FindLoopingSound(file_);
            if (s == null)
            {
                s = new Sound { name = file_, loop = true };
                audio.Add(s);
            }
            s.x = EyesPosX();
            s.y = EyesPosY();
            s.z = EyesPosZ();
        }
        else
        {
            StopLoopingSound(file_);
        }
    }

    private Sound FindLoopingSound(string file_)
    {
        for (int i = 0; i < audio.soundsCount; i++)
        {
            if (audio.sounds[i] != null && audio.sounds[i].name == file_)
                return audio.sounds[i];
        }
        return null;
    }

    private void StopLoopingSound(string file_)
    {
        for (int i = 0; i < audio.soundsCount; i++)
        {
            if (audio.sounds[i] != null && audio.sounds[i].name == file_)
                audio.sounds[i].stop = true;
        }
    }
}