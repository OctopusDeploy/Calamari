using System;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
#if !NET40
using Polly;
#endif

namespace Calamari.Integration.Packages
{
    public class ZipPackageExtractor : SimplePackageExtractor
    {
        public override string[] Extensions { get { return new [] { ".zip"}; } }

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var filesExtracted = 0;
            using (var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var archive = ZipArchive.Open(inStream))
            {
                foreach (var entry in archive.Entries)
                {
                    ProcessEvent(ref filesExtracted, entry, suppressNestedScriptWarning);
                    ExtractEntry(directory, entry);
                }
            }
            return filesExtracted;
        }

        static void ExtractEntry(string directory, ZipArchiveEntry entry)
        {
#if NET40
            entry.WriteToDirectory(directory, new ExtractionOptions {ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
#else
            var extractAttempts = 10;
            Policy.Handle<IOException>().WaitAndRetry(
                    retryCount: extractAttempts,
                    sleepDurationProvider: i => TimeSpan.FromMilliseconds(50),
                    onRetry: (ex, retry) => { Log.Verbose($"Failed to extract: {ex.Message}. Retry in {retry.Milliseconds} milliseconds."); })
                .Execute(() =>
                {
                    entry.WriteToDirectory(directory, new ExtractionOptions {ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
                });
#endif
        }
        
        protected void ProcessEvent(ref int filesExtracted, IEntry entry, bool suppressNestedScriptWarning)
        {
            if (entry.IsDirectory) return;

            filesExtracted++;

            if (!suppressNestedScriptWarning)
            {
                GenericPackageExtractor.WarnIfScriptInSubFolder(entry.Key);
            }
        }
    }
}