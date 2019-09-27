using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Calamari.Tests.Helpers
{
    public static class TarGzBuilder
    {
        public static string BuildSamplePackage(string name, string version)
        {
            var sourceDirectory = TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", name);

            var output = Path.Combine(Path.GetTempPath(), "CalamariTestPackages");
            Directory.CreateDirectory(output);

            var outputTarFilename = Path.Combine(output, name + "." + version + ".tar.gz");
            if (File.Exists(outputTarFilename))
                File.Delete(outputTarFilename);

            using (Stream stream = File.OpenWrite(outputTarFilename))
            using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, new WriterOptions(CompressionType.GZip) {LeaveStreamOpen = false}))
            {
                var isRunningOnWindows = CalamariEnvironment.IsRunningOnWindows;

                var files = Directory.EnumerateFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .Where(f => isRunningOnWindows
                        ? !f.Name.EndsWith(".Nix.nuspec")
                        : !f.Name.EndsWith(".nuspec") || f.Name.EndsWith(".Nix.nuspec"))
                    .Where(f => isRunningOnWindows ? f.Extension != ".sh" : f.Extension != ".ps1");
                foreach(var file in files)
                    writer.Write(GetFilePathRelativeToRoot(sourceDirectory, file), file);
            }
            
            return outputTarFilename;
        }
        
        static string GetFilePathRelativeToRoot(string root, FileInfo file)
        {
            var directory = new DirectoryInfo(root);
            var rootDirectoryUri = new Uri(directory.FullName + Path.DirectorySeparatorChar);
            var fileUri = new Uri(file.FullName);

            return rootDirectoryUri.MakeRelativeUri(fileUri).ToString();
        }
    }
}
