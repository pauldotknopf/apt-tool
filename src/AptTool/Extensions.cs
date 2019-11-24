using System.IO;
namespace AptTool
{
    public static class Extensions
    {
        public static void EnsureDirectoryExists(this string value)
        {
            if (!Directory.Exists(value))
            {
                Directory.CreateDirectory(value);
            }
        }

        public static void EnsureFileExists(this string value)
        {
            if (!File.Exists(value))
            {
                using (File.OpenWrite(value))
                {
                }
            }
        }

        public static string Quoted(this string value)
        {
            return $"\"{value}\"";
        }
    }
}