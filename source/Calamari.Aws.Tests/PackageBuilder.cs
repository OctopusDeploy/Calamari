using System.IO;
using System.IO.Compression;
using NUnit.Framework;

namespace Calamari.Aws.Tests
{
    public class PackageBuilder
    {
        public static string BuildSimpleZip(string name, string version, string directory)
        {
            Assert.That(Directory.Exists(directory), string.Format("Package {0} is not available (expected at {1}).", name, directory));

            var output = Path.Combine(Path.GetTempPath(), "CalamariTestPackages");
            Directory.CreateDirectory(output);
            var path = Path.Combine(output, name + "." + version + ".zip");
            if (File.Exists(path))
                File.Delete(path);

            ZipFile.CreateFromDirectory(directory, path);

            return path;
        }
    }
}
