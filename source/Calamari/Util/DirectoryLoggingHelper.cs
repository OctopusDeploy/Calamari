using Calamari.Integration.FileSystem;
using System.IO;
using System.Linq;
using System.Text;

namespace Calamari.Util
{
    public static class DirectoryLoggingHelper
    {
        public static void LogDirectoryContents(ICalamariFileSystem fileSystem, string workingDirectory, string currentDirectoryRelativePath, int depth = 0)
        {
            var directory = new DirectoryInfo(Path.Combine(workingDirectory, currentDirectoryRelativePath));

            var files = fileSystem.EnumerateFiles(directory.FullName).ToList();
            for (int i = 0; i < files.Count; i++)
            {
                // Only log the first 50 files in each directory
                if (i == 50)
                {
                    Log.VerboseFormat("{0}And {1} more files...", Indent(depth), files.Count - i);
                    break;
                }

                var file = files[i];
                Log.Verbose(Indent(depth) + Path.GetFileName(file));
            }

            foreach (var subDirectory in fileSystem.EnumerateDirectories(directory.FullName).Select(x => new DirectoryInfo(x)))
            {
                Log.Verbose(Indent(depth + 1) + "\\" + subDirectory.Name);
                LogDirectoryContents(fileSystem, workingDirectory, Path.Combine(currentDirectoryRelativePath, subDirectory.Name), depth + 1);
            }
        }

        static string Indent(int n)
        {
            var indent = new StringBuilder("|");
            for (int i = 0; i < n; i++)
                indent.Append("-");

            return indent.ToString();
        }
    }
}
