using System;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Common;

namespace Calamari.Common.Features.Packages
{
    public class PackageExtractionOptions : ExtractionOptions
    {
        readonly ILog log;

        public PackageExtractionOptions(ILog log)
        {
            this.log = log;
            ExtractFullPath = true;
            Overwrite = true;
            PreserveFileTime = true;
            WriteSymbolicLink = WarnThatSymbolicLinksAreNotSupported;
        }

        void WarnThatSymbolicLinksAreNotSupported(string sourcepath, string targetpath)
        {
            log.WarnFormat("Cannot create symbolic link: {0}, Calamari does not currently support the extraction of symbolic links", sourcepath);
        }
    }
}