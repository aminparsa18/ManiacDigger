public class StringUtils
{
    public static string CharArrayToString(int[] charArray, int length)
        => new(Array.ConvertAll(charArray, c => (char)c), 0, length);
}
