using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class HelmChartPackageDownloader: IPackageDownloader
    {
        private static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        const string Extension = ".tgz";
        private readonly IScriptEngine scriptEngine;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ICommandLineRunner commandLineRunner;

        public HelmChartPackageDownloader(IScriptEngine scriptEngine, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }
        
        public PackagePhysicalFileMetadata DownloadPackage(string packageId, IVersion version, string feedId, Uri feedUri,
            ICredentials feedCredentials, bool forcePackageDownload, int maxDownloadAttempts, TimeSpan downloadAttemptBackoff)
        {
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            
            
            if (!forcePackageDownload)
            {
                var downloaded = SourceFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }
            
            return DownloadChart(packageId, version, feedUri, feedCredentials, cacheDirectory);
        }

        private PackagePhysicalFileMetadata DownloadChart(string packageId, IVersion version, Uri feedUri,
            ICredentials feedCredentials, string cacheDirectory)
        {
            var cred = feedCredentials.GetCredential(feedUri, "basic");

            var syntax = new[] {ScriptSyntax.PowerShell, ScriptSyntax.Bash}.First(syntx =>
                scriptEngine.GetSupportedTypes().Contains(syntx));

            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            using (new TemporaryDirectory(tempDirectory))
            {
                var file = GetFetchScript(tempDirectory, syntax);
                var result = scriptEngine.Execute(new Script(file), new CalamariVariableDictionary()
                    {
                        ["Password"] = cred.Password,
                        ["Username"] = cred.UserName,
                        ["Version"] = version.OriginalString,
                        ["Url"] = feedUri.ToString(),
                        ["Package"] = packageId,
                    }, commandLineRunner,
                    new Dictionary<string, string>());
                if (!result.HasErrors)
                {
                    var localDownloadName =
                        Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, Extension));

                    var packageFile = fileSystem.EnumerateFiles(Path.Combine(tempDirectory, "staging")).First();
                    fileSystem.MoveFile(packageFile, localDownloadName);
                    return PackagePhysicalFileMetadata.Build(localDownloadName);
                }
                else
                {
                    throw new Exception("Unable to download chart");
                }
            }
        }

        PackagePhysicalFileMetadata SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, new [] { Extension }));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                if (string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && package.Version.Equals(version))
                {
                    return PackagePhysicalFileMetadata.Build(file, package);
                }
            }

            return null;
        }
        
        
        string GetFetchScript(string workingDirectory, ScriptSyntax syntax)
        {
            AssemblyEmbeddedResources embeddedResources = new AssemblyEmbeddedResources();
            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = "helmFetch.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = "HelmFetch.ps1";
                    break;
                default:
                    throw new InvalidOperationException("No kubernetes context wrapper exists for "+ syntax);
            }
            
            var k8sContextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Integration.Packages.Download.Scripts.{contextFile}");
            fileSystem.OverwriteFile(k8sContextScriptFile, contextScript);
            return k8sContextScriptFile;
        }
    }
}