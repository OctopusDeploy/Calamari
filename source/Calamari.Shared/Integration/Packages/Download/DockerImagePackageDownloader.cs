using System;
using System.Collections.Generic;
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
        const string DockerHubRegistry = "index.docker.io";

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
            var feedHost = GetFeedHost(feedUri);
            var fullImageName =  GetFullImageName(packageId, version, feedUri, feedHost);
            var (username, password) = ExtractCredentials(feedCredentials, feedUri);
            
            PerformPull(username, password, fullImageName, feedHost);
            var (hash, size) = GetImageDetails(fullImageName);
            return new PackagePhysicalFileMetadata(new PackageFileNameMetadata(packageId, version, ""), fullImageName, hash, size );
        }

        static string GetFullImageName(string packageId, IVersion version, Uri feedUri, string feedHost)
        {
            return feedUri.Host.Equals(DockerHubRegistry) ?  $"{packageId}:{version}" : $"{feedHost}/{packageId}:{version}";
        }

        static string GetFeedHost(Uri feedUri)
        {
            if (feedUri.Host.Equals(DockerHubRegistry))
            {
                return string.Empty;
            }

            if (feedUri.Port == 443)
            {
                return feedUri.Host;
            }

            return $"{feedUri.Host}:{feedUri.Port}";
        }

        void PerformPull(string username, string password, string fullImageName, string feed)
        {
            var file = GetFetchScript(scriptEngine);
            using (new TemporaryFile(file))
            {
                var result = scriptEngine.Execute(new Script(file),
                    new CalamariVariableDictionary()
                    {
                        ["DockerUsername"] = username,
                        ["DockerPassword"] = password,
                        ["Image"] = fullImageName,
                        ["FeedUri"] = feed
                    }, commandLineRunner, environmentVariables);
                if (result.ExitCode != 0)
                    throw new CommandException("Unable to pull Docker image");
            }
        }

        (string hash, long size) GetImageDetails(string fullImageName)
        {
            var details = "";
            var result2 = SilentProcessRunner.ExecuteCommand("docker",
                "inspect --format=\"{{.Id}} {{.Size}}\" " + fullImageName,
                ".", environmentVariables, (stdout) => { details = stdout; }, Log.Error);
            if (result2.ExitCode != 0)
            {
                throw new CommandException("Unable to determine acquired docker image hash");
            }

            var parts = details.Split(' ');
            var hash = parts[0];

            // Be more defensive trying to parse the image size.
            // We dont tend to use this property for docker atm anyway so it seems reasonable to ignore if it cant be loaded.
            if (!long.TryParse(parts[1], out var size))
            {
                size = 0;
                Log.Verbose($"Unable to parse image size. ({parts[0]})");
            }


            return (hash, size);
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
        
        
        string GetFetchScript(IScriptEngine scriptEngine)
        {
            var syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();

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
            
            var scriptFile = Path.Combine(".", $"Octopus.{contextFile}");
            var contextScript = new AssemblyEmbeddedResources().GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Integration.Packages.Download.Scripts.{contextFile}");
            fileSystem.OverwriteFile(scriptFile, contextScript);
            return scriptFile;
        }
    }
}