using System;
using System.IO;
using ICSharpCode.SharpZipLib.LZW;

namespace Calamari.Integration.Packages
{
    public class TarLzwPackageExtractor : TarPackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tar.xz", ".txz" }; } }

        protected override Stream GetCompressionStream(Stream stream)
        {
            return new LzwInputStream(stream);
        }
    }
}
