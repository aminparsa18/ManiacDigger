using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace ManicDigger;

public class AudioOpenAl
{
    public GameExit d_GameExit;
    public AudioOpenAl()
    {
        try
        {
            var device = ALC.OpenDevice(null); // null = default device
            if (device == ALDevice.Null)
                throw new Exception("No audio device found.");

            var context = ALC.CreateContext(device, (int[])null);
            ALC.MakeContextCurrent(context);
        }
        catch (Exception e)
        {
            string oalinst = "oalinst.exe";
            if (File.Exists(oalinst))
            {
                try
                {
                    Process.Start(oalinst, "/s");
                }
                catch
                {
                }
            }
            Console.WriteLine(e);
        }
    }

    // Loads a wave/riff audio file.
    public static byte[] LoadWave(Stream stream, out int channels, out int bits, out int rate)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using BinaryReader reader = new(stream);
        // RIFF header
        string signature = new(reader.ReadChars(4));
        if (signature != "RIFF")
            throw new NotSupportedException("Specified stream is not a wave file.");

        int riff_chunck_size = reader.ReadInt32();

        string format = new(reader.ReadChars(4));
        if (format != "WAVE")
            throw new NotSupportedException("Specified stream is not a wave file.");

        // WAVE header
        string format_signature = new(reader.ReadChars(4));
        if (format_signature != "fmt ")
            throw new NotSupportedException("Specified wave file is not supported.");

        int format_chunk_size = reader.ReadInt32();
        int audio_format = reader.ReadInt16();
        int num_channels = reader.ReadInt16();
        int sample_rate = reader.ReadInt32();
        int byte_rate = reader.ReadInt32();
        int block_align = reader.ReadInt16();
        int bits_per_sample = reader.ReadInt16();

        string data_signature = new(reader.ReadChars(4));
        if (data_signature != "data")
            throw new NotSupportedException("Specified wave file is not supported.");

        int data_chunk_size = reader.ReadInt32();

        channels = num_channels;
        bits = bits_per_sample;
        rate = sample_rate;

        return reader.ReadBytes((int)reader.BaseStream.Length);
    }

    public static ALFormat GetSoundFormat(int channels, int bits)
    {
        return channels switch
        {
            1 => bits == 8 ? ALFormat.Mono8 : ALFormat.Mono16,
            2 => bits == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16,
            _ => throw new NotSupportedException("The specified sound format is not supported."),
        };
    }

    public class AudioTask(GameExit gameexit, AudioData sample, AudioOpenAl audio)
    {
        private readonly GameExit gameexit = gameexit;
        private readonly AudioData sample = sample;
        public Vector3 position;

        public void Play()
        {
            if (started)
            {
                shouldplay = true;
                return;
            }
            started = true;
            ThreadPool.QueueUserWorkItem(delegate { play(); });
        }

        private bool started = false;
        private void play()
        {
            try
            {
                DoPlay();
            }
            catch (Exception e) { 
                Console.WriteLine(e.ToString()); 
            }
        }

        private void DoPlay()
        {
            int source = AL.GenSource();

            int buffer = AL.GenBuffer();
            AL.BufferData(buffer, GetSoundFormat(sample.Channels, sample.BitsPerSample), sample.Pcm, sample.Rate);
            //audiofiles[filename]=buffer;

            AL.DistanceModel(ALDistanceModel.InverseDistance);
            AL.Source(source, ALSourcef.RolloffFactor, 0.3f);
            AL.Source(source, ALSourcef.ReferenceDistance, 1);
            AL.Source(source, ALSourcef.MaxDistance, 64 * 1);
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.SourcePlay(source);

            // Query the source to find out when it stops playing.
            for (; ; )
            {
                AL.GetSource(source, ALGetSourcei.SourceState, out int state);
                if ((!loop) && (ALSourceState)state != ALSourceState.Playing)
                {
                    break;
                }
                if (stop)
                {
                    break;
                }
                if (gameexit.exit)
                {
                    break;
                }
                if (loop)
                {
                    if (state == (int)ALSourceState.Playing && (!shouldplay))
                    {
                        AL.SourcePause(source);
                    }
                    if (state != (int)ALSourceState.Playing && shouldplay)
                    {
                        if (restart)
                        {
                            AL.SourceRewind(source);
                            restart = false;
                        }
                        AL.SourcePlay(source);
                    }
                }

                AL.Source(source, ALSource3f.Position, position.X, position.Y, position.Z);
                Thread.Sleep(1);
            }
            Finished = true;
            AL.SourceStop(source);
            AL.DeleteSource(source);
            AL.DeleteBuffer(buffer);
        }

        public bool loop = false;
        private bool stop;
        public void Stop()
        {
            stop = true;
        }
        public bool shouldplay;
        public bool restart;
        public void Restart()
        {
            restart = true;
        }

        internal void Pause()
        {
            shouldplay = false;
        }

        internal bool Finished;
    }

    public static AudioData GetSampleFromArray(byte[] data)
    {
        Stream stream = new MemoryStream(data);
        if (stream.ReadByte() == 'R'
            && stream.ReadByte() == 'I'
            && stream.ReadByte() == 'F'
            && stream.ReadByte() == 'F')
        {
            stream.Position = 0;
            byte[] sound_data = LoadWave(stream, out int channels, out int bits_per_sample, out int sample_rate);
            AudioData sample = new()
            {
                Pcm = sound_data,
                BitsPerSample = bits_per_sample,
                Channels = channels,
                Rate = sample_rate,
            };
            return sample;
        }
        else
        {
            stream.Position = 0;
            AudioData sample = OggDecoder.OggToWav(stream);
            return sample;
        }
    }

    public AudioTask CreateAudio(AudioData sample)
    {
        return new AudioTask(d_GameExit, sample, this);
    }

    public static void UpdateListener(Vector3 position, Vector3 orientation)
    {
        AL.Listener(ALListener3f.Position, position.X, position.Y, position.Z);
        Vector3 up = Vector3.UnitY;
        AL.Listener(ALListenerfv.Orientation, ref orientation, ref up);
    }
}
