using System;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Common;

namespace Calamari.Common.Features.Packages
{
    // SharpCompress 0.48 sealed ExtractionOptions, so we can no longer subclass it.
    // This factory builds a configured ExtractionOptions with Calamari's defaults instead.
    public static class PackageExtractionOptions
    {
        public static ExtractionOptions Create(ILog log, bool extractFullPath = true, bool preserveFileTime = true)
        {
            return new ExtractionOptions
            {
                ExtractFullPath = extractFullPath,
                Overwrite = true,
                PreserveFileTime = preserveFileTime,
                SymbolicLinkHandler = (sourcepath, targetpath) =>
                    log.WarnFormat("Cannot create symbolic link: {0}, Calamari does not currently support the extraction of symbolic links", sourcepath),
            };
        }
    }
}
