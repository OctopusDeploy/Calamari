using System;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Common.Features.Packages.Decorators.ArchiveLimits
{
    public class ArchiveLimitException : InvalidOperationException
    {
        public const string CorruptedHeaderWarning = "The given archive may be corrupt, or a security risk: the archive headers did not match the actual file sizes. As a precaution, those files have been truncated to their declared size. Please see https://oc.to/ArchiveDecompressionLimits for information on why we do this, and how to modify these settings if the archive is legitimate.";

        ArchiveLimitException(string message) 
            : base(message)
        {
        }

        public static ArchiveLimitException FromUncompressedSizeBreach(long totalUncompressedBytes)
        {
            var totalUncompressedText = totalUncompressedBytes.ToFileSizeString();
            return new ArchiveLimitException($"The given archive may be a security risk: it would extract to {totalUncompressedText}. As a precaution, we’ve blocked decompression. Please see https://oc.to/ArchiveDecompressionLimits for information on why we do this, and how to modify these settings if the archive is legitimate.");
        }

        public static ArchiveLimitException FromCompressionRatioBreach(long totalCompressedBytes, long totalUncompressedBytes)
        {
            var totalCompressedText = totalCompressedBytes.ToFileSizeString();
            var totalUncompressedText = totalUncompressedBytes.ToFileSizeString();
            return new ArchiveLimitException($"The given archive may be a security risk: it is {totalCompressedText} compressed, but would extract to {totalUncompressedText}. As a precaution, we’ve blocked decompression. Please see https://oc.to/ArchiveDecompressionLimits for information on why we do this, and how to modify these settings if the archive is legitimate.");
        }
    }
}