using System;
using System.IO;

namespace Calamari.Integration.Packages
{
    public class TarLzwPackageExtractor : TarPackageExtractor
    {
        public TarLzwPackageExtractor(ILog log) : base(log)
        {
        }

        public override string[] Extensions { get { return new[] { ".tar.xz", ".txz" }; } }

        protected override Stream GetCompressionStream(Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
