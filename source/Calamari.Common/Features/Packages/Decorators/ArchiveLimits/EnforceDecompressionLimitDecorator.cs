using System;
using SharpCompress.Archives;

namespace Calamari.Common.Features.Packages.Decorators.ArchiveLimits
{
    /// <summary>
    /// This decorator prevents decompression of archives if they would extract to more than a limit configurable in Octopus Server,
    /// to prevent using too much disk space in scenarios where limited space is available.
    /// </summary>
    public class EnforceDecompressionLimitDecorator : PackageExtractorDecorator
    {
        readonly long maximumUncompressedSize;

        public EnforceDecompressionLimitDecorator(IPackageExtractor concreteExtractor, long maximumUncompressedSize) 
            : base(concreteExtractor)
        {
            this.maximumUncompressedSize = maximumUncompressedSize;
        }

        public override int Extract(string packageFile, string directory)
        {
            if (!TryVerifyUncompressedSize(packageFile, out var exception))
                throw exception!;

            return base.Extract(packageFile, directory);
        }

        bool TryVerifyUncompressedSize(string packageFile, out ArchiveLimitException? exception)
        {
            try
            {
                if (maximumUncompressedSize > 0)
                {
                    using (var archive = ArchiveFactory.Open(packageFile))
                    {
                        var totalUncompressedSize = archive.TotalUncompressSize;

                        if (totalUncompressedSize > maximumUncompressedSize)
                        {
                            exception = ArchiveLimitException.FromUncompressedSizeBreach(totalUncompressedSize);
                            return false;
                        }
                    }
                }
            }
            catch
            {
                // Never fail if something in the detection goes wrong.
            }

            exception = null;
            return true;
        }
    }
}
