using System;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Common;
using SharpCompress.Common.Options;

namespace Calamari.Common.Features.Packages
{
    public class PackageExtractionOptions : IExtractionOptions
    {
        readonly ILog log;

        public PackageExtractionOptions(ILog log)
        {
            this.log = log;
            ExtractFullPath = true;
            Overwrite = true;
            PreserveFileTime = true;
            SymbolicLinkHandler = WarnThatSymbolicLinksAreNotSupported;
        }

        public bool Overwrite { get; set; }
        public bool ExtractFullPath { get; set; }
        public bool PreserveFileTime { get; set; }
        public bool PreserveAttributes { get; set; }
        public int BufferSize { get; set; }
        public Action<string, string>? SymbolicLinkHandler { get; set; }

        void WarnThatSymbolicLinksAreNotSupported(string sourcepath, string targetpath)
        {
            log.WarnFormat("Cannot create symbolic link: {0}, Calamari does not currently support the extraction of symbolic links", sourcepath);
        }

        /// <summary>
        /// For compatibility with SharpCompress methods still requiring <see cref="ExtractionOptions"/>.
        /// </summary>
        internal ExtractionOptions ToExtractionOptions()
        {
            return new ExtractionOptions
            {
                Overwrite = Overwrite,
                ExtractFullPath = ExtractFullPath,
                PreserveFileTime = PreserveFileTime,
                PreserveAttributes = PreserveAttributes,
                BufferSize = BufferSize,
                SymbolicLinkHandler = SymbolicLinkHandler
            };
        }
    }
}