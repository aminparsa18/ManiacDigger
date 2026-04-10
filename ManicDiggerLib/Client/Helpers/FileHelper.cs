public class FileHelper
{
    public static string[] DirectoryGetFiles(string path)
    {
        if (!Directory.Exists(path))
            return [];
        return Directory.GetFiles(path);
    }

    public static byte[] IntArrayToByteArray(int[] input, int inputLength)
    {
        byte[] output = new byte[inputLength * 2];
        Buffer.BlockCopy(input, 0, output, 0, inputLength * 2);
        return output;
    }
}