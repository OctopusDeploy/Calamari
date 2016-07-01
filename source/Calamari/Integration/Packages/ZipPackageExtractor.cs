using System;
using System.IO;
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
            using (var reader = ZipReader.Open(inStream))
            {               
                while (reader.MoveToNextEntry())
                {
                    ProcessEvent(ref filesExtracted, reader.Entry, suppressNestedScriptWarning);
                    reader.WriteEntryToDirectory(directory,ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite | ExtractOptions.PreserveFileTime);
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