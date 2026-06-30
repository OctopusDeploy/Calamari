using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using Polly;
using SharpCompress.Readers;

namespace Calamari.Common.Features.Packages
{
    public class ZipPackageExtractor(ILog log) : IPackageEntryExtractor
    {
        public string[] Extensions => [".zip", ".whl"];

        public int Extract(string packageFile, string directory)
        {
            PackageExtractorUtils.EnsureTargetDirectoryExists(directory);
            
            var filesExtracted = 0;
            using var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read);
            using var archive = ZipArchive.Open(inStream);
            
            foreach (var entry in archive.Entries)
            {
                ProcessEvent(ref filesExtracted, entry);
                ExtractEntry(directory, entry);
            }

            return filesExtracted;
        }

        public void ExtractEntry(string directory, IArchiveEntry entry)
        {
            var strategy = PackageExtractorUtils.CreateIoExceptionRetryStrategy(log);

            strategy.Execute(() => entry.WriteToDirectory(directory, new PackageExtractionOptions(log)));
        }

        void ProcessEvent(ref int filesExtracted, IEntry entry)
        {
            if (entry.IsDirectory)
                return;

            filesExtracted++;
        }
    }
}
