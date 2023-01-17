﻿using System;
using System.IO;
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
                var archiveInfo = new FileInfo(archivePath);
                using (var archive = ArchiveFactory.Open(archivePath))
                {
                    var compressedSize = archiveInfo.Length;
                    var uncompressedSize = archive.TotalUncompressSize;
                    var compressionRatio = compressedSize == 0 ? 0 : (double)uncompressedSize / compressedSize;

                    log.LogMetric("DeploymentPackageCompressedSize", compressedSize, operationId);
                    log.LogMetric("DeploymentPackageUncompressedSize", archive.TotalUncompressSize, operationId);
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
