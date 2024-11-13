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
    public class ZipPackageExtractor : IPackageEntryExtractor
    {
        readonly ILog log;
        readonly bool forceUtf8ZipFiles; //to be removed once transitioned to netcore

        //used during testing only
        public ZipPackageExtractor(ILog log)
        {
            this.log = log;
            this.forceUtf8ZipFiles = false;
        }
        
        public ZipPackageExtractor(ILog log, bool forceUtf8ZipFiles)
        {
            this.log = log;
            this.forceUtf8ZipFiles = forceUtf8ZipFiles;
        }

        public string[] Extensions => new[] { ".zip" };

        public int Extract(string packageFile, string directory)
        {
            PackageExtractorUtils.EnsureTargetDirectoryExists(directory);
            
            var filesExtracted = 0;
            using var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read);
            
#if NETFRAMEWORK
            var readerOptions = new ReaderOptions();
            if (forceUtf8ZipFiles)
            {
                readerOptions.ArchiveEncoding.Forced = System.Text.Encoding.UTF8;
            }

            using var archive = ZipArchive.Open(inStream, readerOptions);
#else
            using var archive = ZipArchive.Open(inStream);
#endif
            
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