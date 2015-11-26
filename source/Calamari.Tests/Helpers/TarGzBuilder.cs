using System.IO;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Calamari.Tests.Helpers
{
    public static class TarGzBuilder
    {

        /// <summary>
        /// Creates a GZipped Tar file from a source directory
        /// </summary>
        /// <param name="outputTarFilename">Output .tar.gz file</param>
        /// <param name="sourceDirectory">Input directory containing files to be added to GZipped tar archive</param>
        public static void BuildSamplePackage(string outputTarFilename, string sourceDirectory)
        {
            using (FileStream fs = new FileStream(outputTarFilename, FileMode.Create, FileAccess.Write, FileShare.None))
            using (Stream gzipStream = new GZipOutputStream(fs))
            using (TarArchive tarArchive = TarArchive.CreateOutputTarArchive(gzipStream))
            {
                var rootPath = sourceDirectory
                    .Replace(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, "")
                    .Replace('\\', '/');
                AddDirectoryFilesToTar2(tarArchive, sourceDirectory, true, rootPath);
            }
        }

        
        private static void AddDirectoryFilesToTar2(TarArchive tarArchive, string sourceDirectory, bool recurse, string rootDirectory)
        {
            // Recursively add sub-folders
            if (recurse)
            {
                string[] directories = Directory.GetDirectories(sourceDirectory);
                foreach (string directory in directories)
                    AddDirectoryFilesToTar2(tarArchive, directory, recurse, rootDirectory);
            }

            // Add files
            string[] filenames = Directory.GetFiles(sourceDirectory);
            foreach (string filename in filenames)
            {
                TarEntry tarEntry = TarEntry.CreateEntryFromFile(filename);

                if (tarEntry.Name.StartsWith(rootDirectory))
                {
                    tarEntry.Name = tarEntry.Name.Substring(rootDirectory.Length+1);
                }


                tarArchive.WriteEntry(tarEntry, true);
            }
        }
    }
}
