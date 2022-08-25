using System;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing;
using Calamari.Testing.Helpers;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Calamari.Tests.Helpers
{
    public static class TarGzBuilder
    {
        public static string BuildSamplePackage(string name, string version, bool excludeNonNativePlatformScripts = true)
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
                        ? !IsNixNuspecFile(f)
                        : !IsNotANuspecFile(f) || IsNixNuspecFile(f));

                if (excludeNonNativePlatformScripts)
                    files = files.Where(f => isRunningOnWindows ? f.Extension != ".sh" : f.Extension != ".ps1");

                foreach(var file in files)
                    writer.Write(GetFilePathRelativeToRoot(sourceDirectory, file), file);
            }
            
            return outputTarFilename;

            bool IsNixNuspecFile(FileInfo f) => f.Name.EndsWith(".Nix.nuspec");
            bool IsNotANuspecFile(FileInfo f) => f.Name.EndsWith(".nuspec");
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
