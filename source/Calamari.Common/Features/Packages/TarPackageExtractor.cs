using System;
using System.IO;
using System.Text;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Retry;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;

namespace Calamari.Common.Features.Packages
{
    public class TarPackageExtractor : IPackageExtractor
    {
        readonly ILog log;

        public TarPackageExtractor(ILog log)
        {
            this.log = log;
        }

        public virtual string[] Extensions => new[] { ".tar" };

        public int Extract(string packageFile, string directory)
        {
            PackageExtractorUtils.EnsureTargetDirectoryExists(directory);

            var files = 0;
            using var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read);
            var compressionStream = GetCompressionStream(inStream);
            try
            {
                using var reader = TarReader.Open(compressionStream, new ReaderOptions { ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 } });
                while (reader.MoveToNextEntry())
                {
                    ProcessEvent(ref files, reader.Entry);
                    ExtractEntry(directory, reader);
                }
            }
            finally
            {
                if (compressionStream != inStream)
                    compressionStream.Dispose();
            }

            return files;
        }

        void ExtractEntry(string directory, TarReader reader)
        {
            var strategy = PackageExtractorUtils.CreateIoExceptionRetryStrategy(log);

            strategy.Execute(() => reader.WriteEntryToDirectory(directory, new PackageExtractionOptions(log)));
        }

        protected virtual Stream GetCompressionStream(Stream stream)
        {
            return stream;
        }

        void ProcessEvent(ref int filesExtracted, IEntry entry)
        {
            if (entry.IsDirectory)
                return;

            filesExtracted++;
        }
    }
}