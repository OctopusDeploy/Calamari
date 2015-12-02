using System.IO;
using ICSharpCode.SharpZipLib.BZip2;

namespace Calamari.Integration.Packages
{
    public class TarBzipPackageExtractor : TarPackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tar.bz2", ".tar.bz", ".tbz" }; } }

        protected override Stream GetCompressionStream(Stream stream)
        {
            return new BZip2InputStream(stream);
        }
    }
}