public class StringUtils
{
    public static string CharArrayToString(int[] charArray, int length)
        => new(Array.ConvertAll(charArray, c => (char)c), 0, length);

    public static bool ReadBool(string str)
    {
        if (str == null)
        {
            return false;
        }
        else
        {
            return str != "0"
                && (str != "false")
                && (str != "False")
                && (str != "FALSE");
        }
    }
}
