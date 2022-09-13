using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Readers.GZip;

namespace Calamari.Common.Features.Packages
{
    public class TarGzipPackageExtractor : TarPackageExtractor
    {
        public TarGzipPackageExtractor(ILog log)
            : base(log)
        {
        }

        public override string[] Extensions => new[] { ".tgz", ".tar.gz", ".tar.Z" };

        protected override Stream GetCompressionStream(Stream stream)
        {
            var gzipReader = GZipReader.Open(stream);
            gzipReader.MoveToNextEntry();
            return gzipReader.OpenEntryStream();
        }
    }
}