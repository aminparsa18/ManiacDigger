public partial class Game
{
    // -------------------------------------------------------------------------
    // One-shot sounds
    // -------------------------------------------------------------------------

    public void PlayAudio(string file)
    {
        if (!AudioEnabled)
            return;

        PlayAudioAt(file, EyesPosX, EyesPosY, EyesPosZ);
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
        if (file == null || !AudioEnabled || AssetsLoadProgress != 1)
            return;

        string file_ = file.Replace(".wav", ".ogg");
        if (GetAssetFileLength(file_) == 0)
        {
            Console.WriteLine(string.Format("File not found: {0}", file));
            return;
        }

        Audio.Add(new Sound { name = file_, x = x, y = y, z = z });
    }

    // -------------------------------------------------------------------------
    // Looping sounds
    // -------------------------------------------------------------------------

    public void AudioPlayLoop(string file, bool play, bool restart)
    {
        if (!AudioEnabled && play)
            return;

        if (AssetsLoadProgress != 1)
            return;

        string file_ = file.Replace(".wav", ".ogg");
        if (GetAssetFileLength(file_) == 0)
        {
            Console.WriteLine(string.Format("File not found: {0}", file));
            return;
        }

        if (play)
        {
            Sound s = FindLoopingSound(file_);
            if (s == null)
            {
                s = new Sound { name = file_, loop = true };
                Audio.Add(s);
            }
            s.x = EyesPosX;
            s.y = EyesPosY;
            s.z = EyesPosZ;
        }
        else
        {
            StopLoopingSound(file_);
        }
    }

    private Sound FindLoopingSound(string file_)
    {
        for (int i = 0; i < Audio.soundsCount; i++)
        {
            if (Audio.sounds[i] != null && Audio.sounds[i].name == file_)
                return Audio.sounds[i];
        }
        return null;
    }

    private void StopLoopingSound(string file_)
    {
        for (int i = 0; i < Audio.soundsCount; i++)
        {
            if (Audio.sounds[i] != null && Audio.sounds[i].name == file_)
                Audio.sounds[i].stop = true;
        }
    }
}