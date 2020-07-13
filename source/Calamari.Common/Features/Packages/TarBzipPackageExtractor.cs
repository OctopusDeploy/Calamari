using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace Calamari.Common.Features.Packages
{
    public class TarBzipPackageExtractor : TarPackageExtractor
    {
        public TarBzipPackageExtractor(ILog log)
            : base(log)
        {
        }

        public override string[] Extensions => new[] { ".tar.bz2", ".tar.bz", ".tbz" };

        protected override Stream GetCompressionStream(Stream stream)
        {
            return new BZip2Stream(stream, CompressionMode.Decompress, false);
        }
    }
}