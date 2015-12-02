using System.IO;
using ICSharpCode.SharpZipLib.GZip;

namespace Calamari.Integration.Packages
{
    public class TarGzipPackageExtractor : TarPackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tgz", ".tar.gz", ".tar.Z" }; } }

        protected override Stream GetCompressionStream(Stream stream)
        {
            return new GZipInputStream(stream);
        }
    }
}
