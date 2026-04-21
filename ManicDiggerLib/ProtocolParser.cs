using System.Text;

public class ProtocolParser
{
    public static string ReadString(Stream stream)
    {
        byte[] bytes = ReadBytes(stream);
        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Reads a length delimited byte array
    /// </summary>
    public static byte[] ReadBytes(Stream stream)
    {
        //VarInt length
        int length = ReadUInt32(stream);

        //Bytes
        byte[] buffer = new byte[length];
        int read = 0;
        while (read < length)
        {
            int r = stream.Read(buffer, read, length - read);
            if (r == 0)
#if !CITO
                throw new InvalidDataException("Expected " + (length - read) + " got " + read);
#else
            return null;
#endif
            read += r;
        }
        return buffer;
    }

    /// <summary>
    /// Writes length delimited byte array
    /// </summary>
    public static void WriteBytes(Stream stream, byte[] val)
    {
        WriteUInt32_(stream, val.Length);
        stream.Write(val, 0, val.Length);
    }
   
    public static Key ReadKey_(byte firstByte, Stream stream)
    {
        if (firstByte < 128)
            return Key.Create(firstByte >> 3, firstByte & 0x07);
        int fieldID = (ReadUInt32(stream) << 4) | ((firstByte >> 3) & 0x0F);
        return Key.Create(fieldID, firstByte & 0x07);
    }

    /// <summary>
    /// Seek past the value for the previously read key.
    /// </summary>
    public static void SkipKey(Stream stream, Key key)
    {
        switch (key.GetWireType())
        {
            case Wire.Fixed32:
                stream.Seek(4, SeekOrigin.Current);
                return;
            case Wire.Fixed64:
                stream.Seek(8, SeekOrigin.Current);
                return;
            case Wire.LengthDelimited:
                stream.Seek(ReadUInt32(stream), SeekOrigin.Current);
                return;
            case Wire.Varint:
                ReadSkipVarInt(stream);
                return;
            default:
#if !CITO
                throw new NotImplementedException("Unknown wire type: " + key.GetWireType());
#else
                return;
#endif
        }
    }

    //namespace SilentOrbit.ProtocolBuffers
    //{
    //public static partial class ProtocolParser
    //{
    /// <summary>
    /// Reads past a varint for an unknown field.
    /// </summary>
    public static void ReadSkipVarInt(Stream stream)
    {
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0)
#if !CITO
                throw new IOException("Stream ended too early");
#else
                return;
#endif

            if ((b & 0x80) == 0)
                return; //end of varint
        }
    }

    /// <summary>
    /// Unsigned VarInt format
    /// Do not use to read int32, use ReadUint64 for that.
    /// </summary>
    public static int ReadUInt32(Stream stream)
    {
        int b;
        int val = 0;

        for (int n = 0; n < 5; n++)
        {
            b = stream.ReadByte();
            if (b < 0)
#if !CITO
                throw new IOException("Stream ended too early");
#else
                return 0;
#endif

            //Check that it fits in 32 bits
            if ((n == 4) && (b & 0xF0) != 0)
#if !CITO
                throw new InvalidDataException("Got larger VarInt than 32bit unsigned");
#else
                return 0;
#endif
            //End of check

            if ((b & 0x80) == 0)
                return val | b << (7 * n);

            val |= (b & 0x7F) << (7 * n);
        }

#if !CITO
        throw new InvalidDataException("Got larger VarInt than 32bit unsigned");
#else
        return 0;
#endif
    }

    /// <summary>
    /// Unsigned VarInt format
    /// </summary>
    public static void WriteUInt32_(Stream stream, int val)
    {
        byte[] buffer = new byte[5];
        int count = 0;

        while (true)
        {
#if !CITO
            buffer[count] = (byte)(val & 0x7F);
#else
            buffer[count] = (val & 0x7F).LowByte;
#endif
            val = val >> 7;
            if (val == 0)
                break;

            buffer[count] |= 0x80;

            count += 1;
        }

        stream.Write(buffer, 0, count + 1);
    }
    //#endregion
   
    /// <summary>
    /// Unsigned VarInt format
    /// </summary>
    public static int ReadUInt64(Stream stream)
    {
        int b;
        int val = 0;

        for (int n = 0; n < 10; n++)
        {
            b = stream.ReadByte();
            if (b < 0)
#if !CITO
                throw new IOException("Stream ended too early");
#else
                return 0;
#endif

            //Check that it fits in 64 bits
            if ((n == 9) && (b & 0xFE) != 0)
#if !CITO
                throw new InvalidDataException("Got larger VarInt than 64 bit unsigned");
#else
                return 0;
#endif
            //End of check

            if ((b & 0x80) == 0)
                //return val | (ulong)b << (7 * n);
                return val | b << (7 * n);

            //val |= (ulong)(b & 0x7F) << (7 * n);
            val |= (b & 0x7F) << (7 * n);
        }
#if !CITO
        throw new InvalidDataException("Got larger VarInt than 64 bit unsigned");
#else
        return 0;
#endif
    }

    /// <summary>
    /// Unsigned VarInt format
    /// </summary>
    public static void WriteUInt64(Stream stream, int val)
    {
        byte[] buffer = new byte[10];
        int count = 0;

        while (true)
        {
#if !CITO
            buffer[count] = (byte)(val & 0x7F);
#else
            buffer[count] = (val & 0x7F).LowByte;
#endif
            val = val >>> 7;
            if (val == 0)
                break;

            buffer[count] |= 0x80;

            count += 1;
        }

        stream.Write(buffer, 0, count + 1);
    }

    //#endregion
    //#region Varint: bool
    public static bool ReadBool(Stream stream)
    {
        int b = stream.ReadByte();
        if (b < 0)
#if !CITO
            throw new IOException("Stream ended too early");
#else
            return false;
#endif
        if (b == 1)
            return true;
        if (b == 0)
            return false;
#if !CITO
        throw new InvalidDataException("Invalid boolean value");
#else
        return false;
#endif
    }

    public static void WriteBool(Stream stream, bool val)
    {
        byte ret = 0;
        if (val)
        {
            ret = 1;
        }
        stream.WriteByte(ret);
    }
    //#endregion
}