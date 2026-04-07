public class FileHelper
{
    public static string[] DirectoryGetFiles(string path)
    {
        if (!Directory.Exists(path))
            return [];
        return Directory.GetFiles(path);
    }
}