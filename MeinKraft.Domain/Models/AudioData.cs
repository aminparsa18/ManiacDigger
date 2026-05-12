/// <summary>Decoded PCM audio data ready to be submitted to an OpenAL buffer.</summary>
public class AudioData
{
    /// <summary>Raw PCM sample bytes.</summary>
    public byte[] Pcm { get; set; }

    /// <summary>Number of audio channels (1 = mono, 2 = stereo).</summary>
    public int Channels { get; set; }

    /// <summary>Sample rate in Hz (e.g. 44100).</summary>
    public int Rate { get; set; }

    /// <summary>Bit depth per sample (typically 8 or 16).</summary>
    public int BitsPerSample { get; set; } = 16;
}