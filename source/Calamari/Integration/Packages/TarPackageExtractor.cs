using System;
using System.IO;
using SharpCompress.Archive.GZip;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Compressor.Deflate;
using SharpCompress.Reader;
using SharpCompress.Reader.Tar;

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
                    using (var reader = TarReader.Open(compressionStream, Options.None))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            ProcessEvent(ref files, reader.Entry, suppressNestedScriptWarning);
                            reader.WriteEntryToDirectory(directory, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
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
                WarnIfScriptInSubFolder(entry.Key);
            }
        }

        void WarnIfScriptInSubFolder(string path)
        {
            var fileName = Path.GetFileName(path);
            if (string.Equals(fileName, "Deploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PreDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PostDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "DeployFailed.ps1", StringComparison.OrdinalIgnoreCase))
            {
                var directoryName = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    Log.WarnFormat("The script file \"{0}\" contained within the package will not be executed because it is contained within a child folder. As of Octopus Deploy 2.4, scripts in sub folders will not be executed.", path);
                }
            }
        }
    }
}