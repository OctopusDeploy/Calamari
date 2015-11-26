using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using Calamari.Util;
using ICSharpCode.SharpZipLib.Tar;

namespace Calamari.Integration.Packages
{
    public class TarPackageExtractor : SimplePackageExtractor
    {
        public override string[] Extensions { get { return new[] { ".tar" }; } }

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            using (var inStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            {
                var compressionStream = GetCompressionStream(inStream);
                try
                {
                    return ExtractTar(directory, compressionStream);
                }
                finally
                {
                    if (compressionStream != inStream)
                    {
                        compressionStream.Dispose();
                    }
                }
            }
        }

        private int ExtractTar(string directory, Stream compressionStream)
        {
            var nonDirectoryFiles = 0;
            using (var tarArchive = TarArchive.CreateInputTarArchive(compressionStream))
            {
                tarArchive.ProgressMessageEvent += (archive, entry, message) => ProcessEvent(ref nonDirectoryFiles, entry);
                tarArchive.ExtractContents(directory);
            }
            return nonDirectoryFiles;
        }

        protected virtual Stream GetCompressionStream(Stream stream)
        {
            return stream;
        }

        protected void ProcessEvent(ref int filesExtracted, TarEntry entry)
        {
            if (!entry.IsDirectory)
                filesExtracted++;
        }
    }
}