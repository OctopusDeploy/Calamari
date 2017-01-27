using System.IO;
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

            var outputTarFilename = Path.Combine(Path.GetTempPath(), name + "." + version + ".tar.gz");
            if (File.Exists(outputTarFilename))
                File.Delete(outputTarFilename);

            using (Stream stream = File.OpenWrite(outputTarFilename))
            using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, new WriterOptions(CompressionType.GZip) {LeaveStreamOpen = false}))
            {
                writer.WriteAll(sourceDirectory, "*", SearchOption.AllDirectories);
            }
            
            return outputTarFilename;
        }
    }
}
