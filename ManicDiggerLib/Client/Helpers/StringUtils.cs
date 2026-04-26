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
                && (!str.Equals(bool.FalseString, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    public static bool IsChecksum(string checksum)
    {
        //Check if checksum string has correct length
        if (checksum.Length != 32)
        {
            return false;
        }
        //Convert checksum string to lowercase letters
        checksum = checksum.ToLower();
        char[] chars = checksum.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if ((chars[i] < '0' || chars[i] > '9') && (chars[i] < 'a' || chars[i] > 'f'))
            {
                //Return false if any character inside the checksum is not hexadecimal
                return false;
            }
        }
        //Return true if all checks have been passed
        return true;
    }

    public static string DecodeHTMLEntities(string htmlEncodedString)
    {
        return System.Web.HttpUtility.HtmlDecode(htmlEncodedString);
    }
}
