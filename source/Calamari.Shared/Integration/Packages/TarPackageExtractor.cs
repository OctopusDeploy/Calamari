using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;

namespace Calamari.Integration.Packages
{
    public class TarPackageExtractor : SimplePackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tar" }; } }

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
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
                            if (IsExcludedPath(reader.Entry.Key)) continue;
                            
                            ProcessEvent(ref files, reader.Entry, suppressNestedScriptWarning);
                            reader.WriteEntryToDirectory(directory, new ExtractionOptions {ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
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
        

        protected virtual Stream GetCompressionStream(Stream stream)
        {
            return stream;
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