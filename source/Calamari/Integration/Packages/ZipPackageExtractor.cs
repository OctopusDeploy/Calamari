using System;
using System.IO;
using System.Linq;
using SharpCompress.Archive;
using SharpCompress.Archive.Zip;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Reader.Zip;

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
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    ProcessEvent(ref filesExtracted, entry, suppressNestedScriptWarning);
                    entry.WriteToDirectory(directory, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite | ExtractOptions.PreserveFileTime);
                }
            }
            return filesExtracted;
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