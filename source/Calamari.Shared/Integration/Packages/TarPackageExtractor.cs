using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
#if !NET40
using Polly;
#endif

namespace Calamari.Integration.Packages
{
    public class TarPackageExtractor : IPackageExtractor
    {
        readonly ILog log;
        public virtual string[] Extensions => new[] { ".tar" };

        public TarPackageExtractor(ILog log)
        {
            this.log = log;
        }
        
        public int Extract(string packageFile, string directory)
        {
            var files = 0;
            using (var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            {
                var compressionStream = GetCompressionStream(inStream);
                try
                {
                    using (var reader = TarReader.Open(compressionStream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            ProcessEvent(ref files, reader.Entry);
                            ExtractEntry(directory, reader);
                        }
                    }
                }
                finally
                {
                    if (compressionStream != inStream)
                    {
                        compressionStream.Dispose();
                    }
                }
            }
            return files;
        }

        void ExtractEntry(string directory, TarReader reader)
        {
#if NET40
            reader.WriteEntryToDirectory(directory, new PackageExtractionOptions());
#else
            var extractAttempts = 10;
            Policy.Handle<IOException>().WaitAndRetry(
                    retryCount: extractAttempts,
                    sleepDurationProvider: i => TimeSpan.FromMilliseconds(50),
                    onRetry: (ex, retry) => { log.Verbose($"Failed to extract: {ex.Message}. Retry in {retry.Milliseconds} milliseconds."); })
                .Execute(() =>
                {
                    reader.WriteEntryToDirectory(directory, new PackageExtractionOptions());
                });
#endif
        }

        protected virtual Stream GetCompressionStream(Stream stream)
        {
            return stream;
        }

        protected void ProcessEvent(ref int filesExtracted, IEntry entry)
        {
            if (entry.IsDirectory) return;

            filesExtracted++;
        }
    }
}