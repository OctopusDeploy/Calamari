using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class DockerImagePackageDownloader: IPackageDownloader
    {
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        // Ensures that any credential details are only available for the duration of the acquisition
        readonly Dictionary<string, string> environmentVariables = new Dictionary<string, string>()
        {
            {"DOCKER_CONFIG", "./octo-docker-configs"}
        };
        
        public DockerImagePackageDownloader(IScriptEngine scriptEngine, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }
        
        public PackagePhysicalFileMetadata DownloadPackage(string packageId, IVersion version, string feedId, Uri feedUri,
            ICredentials feedCredentials, bool forcePackageDownload, int maxDownloadAttempts, TimeSpan downloadAttemptBackoff)
        {
            //Always try re-pull image, docker engine can take care of the rest
            var feed = feedUri.Port == 443 ? feedUri.Host : $"{feedUri.Host}:{feedUri.Port}";
            var fullImageName = $"{feed}/{packageId}:{version}";
            var (username, password) = ExtractCredentials(feedCredentials, feedUri);
            
            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            using (new TemporaryDirectory(tempDirectory))
            {
                PerformPull(tempDirectory, username, password, fullImageName, feed);
                var (hash, size) = GetImageDetails(fullImageName, tempDirectory);
                return new PackagePhysicalFileMetadata(new PackageFileNameMetadata(packageId, version, ""), fullImageName, hash, size );
            }
        }

        void PerformPull(string tempDirectory, string username, string password, string fullImageName, string feed)
        {
            var file = GetFetchScript(tempDirectory, scriptEngine);
            var result = scriptEngine.Execute(new Script(file),
                new CalamariVariableDictionary()
                {
                    ["DockerUsername"] = username,
                    ["DockerPassword"] = password,
                    ["Image"] = fullImageName,
                    ["FeedUri"] = feed
                }, commandLineRunner, environmentVariables);
            if (result.HasErrors)
                throw new CommandException("Unable to pull Docker image");
        }

        (string hash, long size) GetImageDetails(string fullImageName, string tempDirectory)
        {
            var details = "";
            var result2 = SilentProcessRunner.ExecuteCommand("docker",
                "inspect --format=\"{{.Id}} {{.Size}}\" " + fullImageName,
                tempDirectory, environmentVariables, (stdout) => { details = stdout; }, Log.Error);
            if (result2.ExitCode != 0)
            {
                throw new CommandException("Unable to determine acquired docker image hash");
            }

            var parts = details.Split(' ');


            return (parts[0], long.Parse(parts[1]));
        }


        (string username, string password) ExtractCredentials(ICredentials feedCredentials, Uri feedUri)
        {
            if (feedCredentials == null)
            {
                return (null, null);
            }
            var creds = feedCredentials.GetCredential(feedUri, "basic");
            return (creds.UserName, creds.Password);
        }
        
        
        string GetFetchScript(string workingDirectory, IScriptEngine scriptEngine)
        {
            var syntax = new[] {ScriptSyntax.PowerShell, ScriptSyntax.Bash}
                .First(syntx => scriptEngine.GetSupportedTypes().Contains(syntx));
            
            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = "DockerPull.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = "DockerPull.ps1";
                    break;
                default:
                    throw new InvalidOperationException("No kubernetes context wrapper exists for "+ syntax);
            }
            
            var scriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = new AssemblyEmbeddedResources().GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Integration.Packages.Download.Scripts.{contextFile}");
            fileSystem.OverwriteFile(scriptFile, contextScript);
            return scriptFile;
        }
    }
}