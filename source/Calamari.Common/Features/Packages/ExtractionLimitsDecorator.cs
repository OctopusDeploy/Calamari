using System.IO;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Calamari.Common.Features.Packages
{
    public class ExtractionLimitsDecorator : IPackageExtractor
    {
        public IPackageExtractor ConcreteExtractor { get; }
        readonly ILog log;
        public string[] Extensions => ConcreteExtractor.Extensions;

        public ExtractionLimitsDecorator(IPackageExtractor concreteExtractor, ILog log)
        {
            ConcreteExtractor = concreteExtractor;
            this.log = log;
        }
        public int Extract(string packageFile, string directory)
        {
            using (var timer = log.BeginTimedOperation($"Extract package"))
            {
                LogArchiveMetrics(packageFile, timer.OperationId);

                var result = ConcreteExtractor.Extract(packageFile, directory);
                timer.Complete();

                return result;
            }
        }

        void LogArchiveMetrics(string archivePath, string operationId)
        {
            try
            {
                using (var archiveDetails = ArchiveFactory.Open(archivePath))
                {
                    var compressionRatio = archiveDetails.TotalUncompressSize == 0 ? 0 : (double)archiveDetails.TotalSize / archiveDetails.TotalUncompressSize;

                    log.LogMetric("DeploymentPackageCompressedSize", archiveDetails.TotalSize, operationId);
                    log.LogMetric("DeploymentPackageUncompressedSize", archiveDetails.TotalUncompressSize, operationId);
                    log.LogMetric("DeploymentPackageCompressionRatio", compressionRatio, operationId);
                }
            }
            catch
            {
                log.Verbose("Could not collect archive metrics");
            }
        }
    }

    public static class ExtractionLimitsExtensions
    {
        public static IPackageExtractor WithExtractionLimits(this IPackageExtractor concreteExtractor, ILog log)
        {
            return new ExtractionLimitsDecorator(concreteExtractor, log);
        }
    }
}
