using System;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Common;

namespace Calamari.Common.Features.Packages
{
    public class PackageExtractionOptions
    {
        readonly ILog log;

        public PackageExtractionOptions(ILog log)
        {
            this.log = log;
        }

        public bool ExtractFullPath { get; set; } = true;
        public bool Overwrite { get; set; } = true;
        public bool PreserveFileTime { get; set; } = true;

        public static implicit operator ExtractionOptions(PackageExtractionOptions options)
            => new ExtractionOptions
            {
                ExtractFullPath = options.ExtractFullPath,
                Overwrite = options.Overwrite,
                PreserveFileTime = options.PreserveFileTime,
                SymbolicLinkHandler = options.WarnThatSymbolicLinksAreNotSupported
            };

        void WarnThatSymbolicLinksAreNotSupported(string sourcepath, string targetpath)
        {
            log.WarnFormat("Cannot create symbolic link: {0}, Calamari does not currently support the extraction of symbolic links", sourcepath);
        }
    }
}
