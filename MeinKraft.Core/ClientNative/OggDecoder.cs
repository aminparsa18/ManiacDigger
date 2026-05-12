using NVorbis;
using System.Buffers.Binary;

/// <summary>
/// Decodes Ogg Vorbis audio streams into raw 16-bit signed PCM <see cref="AudioData"/>.
/// </summary>
public static class OggDecoder
{
    private const int ReadBufferSamples = 4096; // samples per channel per read

    /// <summary>
    /// Decodes an Ogg Vorbis <paramref name="ogg"/> stream into interleaved
    /// 16-bit signed little-endian PCM stored in <see cref="AudioData.Pcm"/>.
    /// </summary>
    /// <param name="ogg">A readable stream positioned at the start of an Ogg Vorbis file.</param>
    /// <returns>
    /// An <see cref="AudioData"/> instance with <see cref="AudioData.Channels"/>,
    /// <see cref="AudioData.Rate"/>, <see cref="AudioData.BitsPerSample"/> (always 16),
    /// and <see cref="AudioData.Pcm"/> populated.
    /// </returns>
    public static AudioData OggToWav(Stream ogg)
    {
        using VorbisReader reader = new(ogg, closeOnDispose: false);

        int channels = reader.Channels;
        int frameSize = ReadBufferSamples * channels;

        float[] floatBuf = new float[frameSize];
        byte[] pcmChunk = new byte[frameSize * 2];   // reused each iteration

        using MemoryStream output = new();

        int samplesRead;
        while ((samplesRead = reader.ReadSamples(floatBuf, 0, frameSize)) > 0)
        {
            int byteCount = samplesRead * 2;
            Span<byte> pcmSpan = pcmChunk.AsSpan(0, byteCount);

            for (int i = 0; i < samplesRead; i++)
            {
                // Clamp to [-1, 1], scale to int16, guard exact -1.0 overflow.
                short s = (short)Math.Clamp((int)(floatBuf[i] * 32767f), -32768, 32767);
                BinaryPrimitives.WriteInt16LittleEndian(pcmSpan.Slice(i * 2), s);
            }

            output.Write(pcmChunk, 0, byteCount);
        }

        return new AudioData
        {
            Pcm = output.ToArray(),
            Channels = channels,
            Rate = reader.SampleRate,
            BitsPerSample = 16,
        };
    }
}