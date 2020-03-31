using System.IO;
using SharpCompress.Readers.GZip;

namespace Calamari.Integration.Packages
{
    public class TarGzipPackageExtractor : TarPackageExtractor
    {
        public TarGzipPackageExtractor(ILog log) : base(log)
        {
        }

        public override string[] Extensions => new[] {".tgz", ".tar.gz", ".tar.Z"};

        protected override Stream GetCompressionStream(Stream stream)
        {
            var gzipReader = GZipReader.Open(stream);
            gzipReader.MoveToNextEntry();
            return gzipReader.OpenEntryStream();
        }
    }
}
