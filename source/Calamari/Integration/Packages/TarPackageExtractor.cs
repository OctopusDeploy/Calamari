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
                    return ExtractTar(directory, compressionStream, suppressNestedScriptWarning);
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

        private int ExtractTar(string directory, Stream compressionStream, bool suppressNestedScriptWarning)
        {
            var nonDirectoryFiles = 0;
            using (var tarArchive = TarArchive.CreateInputTarArchive(compressionStream))
            {
                tarArchive.ProgressMessageEvent += (archive, entry, message) => ProcessEvent(ref nonDirectoryFiles, entry, suppressNestedScriptWarning);
                tarArchive.ExtractContents(directory);
            }
            return nonDirectoryFiles;
        }

        protected virtual Stream GetCompressionStream(Stream stream)
        {
            return stream;
        }

        protected void ProcessEvent(ref int filesExtracted, TarEntry entry, bool suppressNestedScriptWarning)
        {
            if (!entry.IsDirectory)
            {
                filesExtracted++;

                if (!suppressNestedScriptWarning)
                {
                    WarnIfScriptInSubFolder(entry.Name);
                }
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