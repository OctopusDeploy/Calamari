using System;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Archives;

namespace Calamari.Common.Features.Packages.Decorators.Logging
{
    public class LogArchiveMetricsDecorator : PackageExtractorDecorator
    {
        readonly ILog log;

        public LogArchiveMetricsDecorator(IPackageExtractor concreteExtractor, ILog log)
            : base(concreteExtractor)
        {
            this.log = log;
        }

        public override int Extract(string packageFile, string directory)
        {
            using (var timer = log.BeginTimedOperation($"Extract package"))
            {
                LogArchiveMetrics(packageFile, timer.OperationId);

                var result = base.Extract(packageFile, directory);
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
}
