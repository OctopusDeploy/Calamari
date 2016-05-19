using System;
using System.IO;
using SharpCompress.Archive;
using SharpCompress.Compressor.LZMA;


namespace Calamari.Integration.Packages
{
    public class TarLzwPackageExtractor : TarPackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tar.xz", ".txz" }; } }

        protected override Stream GetCompressionStream(Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
