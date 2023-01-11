using System;
using Calamari.Common.Features.Packages.Decorators.ArchiveLimits;
using Calamari.Common.Features.Packages.Decorators.Logging;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Packages.Decorators
{
    public static class ArchiveLimitsExtensions
    {
        public static IPackageExtractor WithExtractionLimits(this IPackageExtractor extractor, ILog log, IVariables variables)
        {
            var enforceLimits = variables.GetFlag(KnownVariables.Package.ArchiveLimits.Enabled);
            var logMetrics = variables.GetFlag(KnownVariables.Package.ArchiveLimits.MetricsEnabled);

            var result = extractor;

            if (enforceLimits) result = result.WithUncompressedSizeProtection(variables);
            if (enforceLimits) result = result.WithCompressionRatioProtection(variables);
            if (logMetrics) result = result.WithMetricLogging(log);

            return result;
        }

        static IPackageExtractor WithUncompressedSizeProtection(this IPackageExtractor extractor, IVariables variables)
        {
            try
            {
                if (!variables.IsSet(KnownVariables.Package.ArchiveLimits.MaximumUncompressedSize))
                    return extractor;
            
                var maximumUncompressedSize = Convert.ToInt64(variables.Get(KnownVariables.Package.ArchiveLimits.MaximumUncompressedSize));
                return new EnforceDecompressionLimitDecorator(extractor, maximumUncompressedSize);
            }
            catch
            {
                return extractor;
            }
        }

        static IPackageExtractor WithCompressionRatioProtection(this IPackageExtractor extractor, IVariables variables)
        {
            try
            {
                if (!variables.IsSet(KnownVariables.Package.ArchiveLimits.MaximumCompressionRatio))
                    return extractor;

                var maximumCompressionRatio = Convert.ToInt32(variables.Get(KnownVariables.Package.ArchiveLimits.MaximumCompressionRatio));
                return new EnforceCompressionRatioDecorator(extractor, maximumCompressionRatio);
            }
            catch
            {
                return extractor;
            }
        }

        static IPackageExtractor WithMetricLogging(this IPackageExtractor extractor, ILog log)
        {
            return new LogArchiveMetricsDecorator(extractor, log);
        }
    }
}
