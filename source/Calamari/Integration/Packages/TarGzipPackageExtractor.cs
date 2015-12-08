using System.IO;
using System.Linq;
using SharpCompress.Archive;
using SharpCompress.Archive.GZip;
using SharpCompress.Compressor;
using SharpCompress.Compressor.Deflate;
using SharpCompress.Compressor.LZMA;
using SharpCompress.Reader.GZip;

namespace Calamari.Integration.Packages
{
    public class TarGzipPackageExtractor : TarPackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tgz", ".tar.gz", ".tar.Z" }; } }

        protected override Stream GetCompressionStream(Stream stream)
        {
            var gzipReader = GZipReader.Open(stream);
            gzipReader.MoveToNextEntry();
            return gzipReader.OpenEntryStream();
        }
    }
}
