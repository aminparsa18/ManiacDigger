using NVorbis;

public class AudioDataCs : AudioData
{
    public byte[] Pcm;
    public int Channels;
    public int Rate;
    public int BitsPerSample = 16;
}

public class OggDecoder
{
    private const int ReadBufferSamples = 4096; // samples per channel per read

    public AudioDataCs OggToWav(Stream ogg)
    {
        AudioDataCs sample = new();

        using var reader = new VorbisReader(ogg, closeOnDispose: false);

        sample.Channels = reader.Channels;
        sample.Rate = reader.SampleRate;

        // NVorbis gives interleaved floats: [L0, R0, L1, R1, ...]
        // We read chunks and convert each float to 16-bit signed PCM.
        int frameSize = ReadBufferSamples * reader.Channels;
        float[] floatBuf = new float[frameSize];

        using var output = new MemoryStream();

        int samplesRead;
        while ((samplesRead = reader.ReadSamples(floatBuf, 0, frameSize)) > 0)
        {
            // Each float sample → 2 bytes (little-endian signed 16-bit)
            byte[] pcmChunk = new byte[samplesRead * 2];
            for (int i = 0; i < samplesRead; i++)
            {
                // Clamp to [-1, 1] then scale to int16 range
                float f = floatBuf[i];
                if (f > 1f) f = 1f;
                if (f < -1f) f = -1f;

                int val = (int)(f * 32767f);
                // Guard against edge-case overflow at exactly -1.0
                if (val < -32768) val = -32768;

                // Little-endian
                pcmChunk[i * 2] = (byte)(val & 0xFF);
                pcmChunk[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            output.Write(pcmChunk, 0, pcmChunk.Length);
        }

        sample.Pcm = output.ToArray();
        return sample;
    }
}