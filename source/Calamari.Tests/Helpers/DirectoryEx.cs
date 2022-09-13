using System.IO;

namespace Calamari.Tests.Helpers
{
    public static class DirectoryEx
    {
        public static void Copy(string sourcePath, string destPath)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            foreach (var file in Directory.EnumerateFiles(sourcePath))
            {
                var dest = Path.Combine(destPath, Path.GetFileName(file));
                File.Copy(file, dest);
            }

            foreach (var folder in Directory.EnumerateDirectories(sourcePath))
            {
                var dest = Path.Combine(destPath, Path.GetFileName(folder));
                Copy(folder, dest);
            }
        }
    }
}