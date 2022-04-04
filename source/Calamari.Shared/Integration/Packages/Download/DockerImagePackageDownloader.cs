using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    // Note about moving this class: GetFetchScript method uses the namespace of this class as part of the
    // get Embedded Resource to find the DockerPull scripts. If you move this file, be sure look at that method
    // and make sure it can still find the scripts
    public class DockerImagePackageDownloader : IPackageDownloader
    {
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly IVariables variables;
        const string DockerHubRegistry = "index.docker.io";

        // Ensures that any credential details are only available for the duration of the acquisition
        readonly Dictionary<string, string> environmentVariables = new Dictionary<string, string>()
        {
            {
                "DOCKER_CONFIG", "./octo-docker-configs"
            }
        };

        public DockerImagePackageDownloader(IScriptEngine scriptEngine, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, IVariables variables)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
        }

        public PackagePhysicalFileMetadata DownloadPackage(string packageId,
                                                           IVersion version,
                                                           string feedId,
                                                           Uri feedUri,
                                                           string? username,
                                                           string? password,
                                                           bool forcePackageDownload,
                                                           int maxDownloadAttempts,
                                                           TimeSpan downloadAttemptBackoff)
        {
            //Always try re-pull image, docker engine can take care of the rest
            var fullImageName = GetFullImageName(packageId, version, feedUri);

            var feedHost = GetFeedHost(feedUri);
            PerformPull(username, password, fullImageName, feedHost);
            var (hash, size) = GetImageDetails(fullImageName);
            return new PackagePhysicalFileMetadata(new PackageFileNameMetadata(packageId, version, ""), string.Empty, hash, size);
        }

        static string GetFullImageName(string packageId, IVersion version, Uri feedUri)
        {
            return feedUri.Host.Equals(DockerHubRegistry)
                ? $"{packageId}:{version}"
                : $"{feedUri.Authority}{feedUri.AbsolutePath.TrimEnd('/')}/{packageId}:{version}";
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

        void PerformPull(string? username, string? password, string fullImageName, string feed)
        {
            var file = GetFetchScript();
            using (new TemporaryFile(file))
            {
                var clone = variables.Clone();
                clone["DockerUsername"] = username;
                clone["DockerPassword"] = password;
                clone["Image"] = fullImageName;
                clone["FeedUri"] = feed;

                var result = scriptEngine.Execute(new Script(file), clone, commandLineRunner, environmentVariables);
                if (result == null)
                    throw new CommandException("Null result attempting to pull Docker image");
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

        string GetFetchScript()
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
                    throw new InvalidOperationException("No kubernetes context wrapper exists for " + syntax);
            }

            var scriptFile = Path.Combine(".", $"Octopus.{contextFile}");
            var contextScript = new AssemblyEmbeddedResources().GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"{typeof (DockerImagePackageDownloader).Namespace}.Scripts.{contextFile}");
            fileSystem.OverwriteFile(scriptFile, contextScript);
            return scriptFile;
        }

        async Task<bool> IsDigestCached(string image)
        {
            var fullImageName = "blah";
            var digest = "";
            var result = SilentProcessRunner.ExecuteCommand("docker",
                                                            "image inspect --format=\"{{index .RepoDigests 0}}\" " + fullImageName,
                                                            ".", environmentVariables,
                                                            (stdout) => { digest = GetDigest(stdout); }, Log.Error);
            if (String.IsNullOrEmpty(digest))
            {
                return false;
            }

            var url = $"https://index.docker.io/{image}";
            using var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                dynamic payload = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                IList<string> tags = ((IEnumerable<dynamic>)payload.results).Select(result => (string)result.name).ToList();
                var latestImages = new Dictionary<string, string>();
            }
        }

        string GetDigest(string result)
        {
            var tokens = result.Split("@");
            if (tokens.Length > 1)
            {
                return tokens[1];
            }

            return "";
        }
    }
}
