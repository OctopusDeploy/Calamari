using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using Polly;

namespace Calamari.Common.Features.Packages
{
    public class ZipPackageExtractor : IPackageEntryExtractor
    {
        readonly ILog log;

        public ZipPackageExtractor(ILog log)
        {
            this.log = log;
        }

        public string[] Extensions => new[] { ".zip" };

        public int Extract(string packageFile, string directory)
        {
            var filesExtracted = 0;
            using (var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var archive = ZipArchive.Open(inStream))
            {
                foreach (var entry in archive.Entries)
                {
                    ProcessEvent(ref filesExtracted, entry);
                    ExtractEntry(directory, entry);
                }
            }

            return filesExtracted;
        }

        public void ExtractEntry(string directory, IArchiveEntry entry)
        {
            var extractAttempts = 10;
            Policy.Handle<IOException>()
                .WaitAndRetry(
                    extractAttempts,
                    i => TimeSpan.FromMilliseconds(50),
                    (ex, retry) =>
                    {
                        log.Verbose($"Failed to extract: {ex.Message}. Retry in {retry.Milliseconds} milliseconds.");
                    })
                .Execute(() =>
                {
                    entry.WriteToDirectory(directory, new PackageExtractionOptions(log));
                });
        }

        protected void ProcessEvent(ref int filesExtracted, IEntry entry)
        {
            if (entry.IsDirectory)
                return;

            filesExtracted++;
        }
    }
}