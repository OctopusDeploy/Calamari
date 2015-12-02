using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;


namespace Calamari.Integration.Packages
{
    public class ZipPackageExtractor : SimplePackageExtractor
    {
        public override string[] Extensions { get { return new [] { ".zip"}; } }

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var fileExtractionCount = 0;
            var fastZip = new FastZip(new FastZipEvents()
            {
                CompletedFile = (sender, args) =>
                {
                    fileExtractionCount++;
                    WarnIfScriptInSubFolder(args.Name);
                }
            });
            fastZip.ExtractZip(packageFile, directory, string.Empty);
            return fileExtractionCount;
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