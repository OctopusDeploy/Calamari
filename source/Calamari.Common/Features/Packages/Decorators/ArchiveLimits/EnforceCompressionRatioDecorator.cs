using System;
using SharpCompress.Archives;

namespace Calamari.Common.Features.Packages.Decorators.ArchiveLimits
{
    /// <summary>
    /// This decorator prevents extraction of an archive if it would exceed the theoretical limits of current compression algorithms.
    /// </summary>
    public class EnforceCompressionRatioDecorator : PackageExtractorDecorator
    {
        readonly int maximumCompressionRatio;

        public EnforceCompressionRatioDecorator(IPackageExtractor concreteExtractor, int maximumCompressionRatio) 
            : base(concreteExtractor)
        {
            this.maximumCompressionRatio = maximumCompressionRatio;
        }

        public override int Extract(string packageFile, string directory)
        {
            if (!TryVerifyCompressionRatio(packageFile, out var archiveLimitException))
                throw archiveLimitException!;

            return base.Extract(packageFile, directory);
        }

        bool TryVerifyCompressionRatio(string packageFile, out ArchiveLimitException? exception)
        {
            try
            {
                if (maximumCompressionRatio >= 1)
                {
                    using (var archive = ArchiveFactory.Open(packageFile))
                    {
                        var uncompressedSize = archive.TotalUncompressSize;
                        var compressedSize = archive.TotalSize;
                        var compressionRatio = uncompressedSize == 0 ? 0 : uncompressedSize / compressedSize;

                        if (compressionRatio > maximumCompressionRatio)
                        {
                            exception = ArchiveLimitException.FromCompressionRatioBreach(compressedSize, uncompressedSize);
                            return false;
                        }
                    }
                }
            }
            catch
            {
                // Never fail based on failing to calculate the limits.
            }

            exception = null;
            return true;
        }
    }
}
