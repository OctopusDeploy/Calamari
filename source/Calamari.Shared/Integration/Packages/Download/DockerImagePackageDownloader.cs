using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    // Note about moving this class: the ScriptExtractor.GetScript method uses the namespace of this class as part of the	
    // get Embedded Resource to find the DockerLogin and DockerPull scripts. If you move this file, be sure look at that method	
    // and make sure it can still find the scripts
    public class DockerImagePackageDownloader : IPackageDownloader
    {
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly IVariables variables;
        readonly ILog log;
        readonly IFeedLoginDetailsProviderFactory feedLoginDetailsProviderFactory;
        readonly bool useCredentialHelper;
        readonly DockerCredentialHelper dockerCredentialHelper;
        const string DockerHubRegistry = "index.docker.io";
        
        static readonly HashSet<FeedType> SupportedLoginDetailsFeedTypes = new HashSet<FeedType>
        {
            FeedType.AwsElasticContainerRegistry,
            FeedType.AzureContainerRegistry,
            FeedType.GoogleContainerRegistry
        };

        const string DockerConfigFolder = "./octo-docker-configs";

        // Ensures that any credential details are only available for the duration of the acquisition
        readonly Dictionary<string, string> environmentVariables = new Dictionary<string, string>()
        {
            {
                "DOCKER_CONFIG", DockerConfigFolder
            }
        };

        public DockerImagePackageDownloader(IScriptEngine scriptEngine,
                                            ICalamariFileSystem fileSystem,
                                            ICommandLineRunner commandLineRunner,
                                            IVariables variables,
                                            ILog log,
                                            IFeedLoginDetailsProviderFactory feedLoginDetailsProviderFactory)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
            this.log = log;
            this.feedLoginDetailsProviderFactory = feedLoginDetailsProviderFactory;
            this.useCredentialHelper = OctopusFeatureToggles.UseDockerCredentialHelperFeatureToggle.IsEnabled(variables);
            this.dockerCredentialHelper = new DockerCredentialHelper(fileSystem, log);
        }

        (string Username, string Password, Uri FeedUri) GetContainerRegistryLoginDetails(string feedTypeStr, string username, string password, Uri feedUri)
        {
            if (Enum.TryParse(feedTypeStr, out FeedType feedType))
            {
                var feedLoginDetailsProvider = feedLoginDetailsProviderFactory.GetFeedLoginDetailsProvider(feedType);
                return feedLoginDetailsProvider.GetFeedLoginDetails(variables, username, password, feedUri).GetAwaiter().GetResult();
            }
            throw new ArgumentException($"Invalid feed type: {feedTypeStr}");
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
            var contributedFeedType = variables.Get(AuthenticationVariables.FeedType);
            if (Enum.TryParse(contributedFeedType, ignoreCase: true, out FeedType feedType) &&
                SupportedLoginDetailsFeedTypes.Contains(feedType))
            {
                var loginDetails = GetContainerRegistryLoginDetails(contributedFeedType, username, password, feedUri);
                username = loginDetails.Username;
                password = loginDetails.Password;
                feedUri = loginDetails.FeedUri;
            }
            
            //Always try re-pull image, docker engine can take care of the rest
            var fullImageName = GetFullImageName(packageId, version, feedUri);

            var feedHost = GetFeedHost(feedUri);

            var strategy = PackageDownloaderRetryUtils.CreateRetryStrategy<CommandException>(maxDownloadAttempts, downloadAttemptBackoff, log);

            if (useCredentialHelper && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                strategy.Execute(() => dockerCredentialHelper.SetupCredentialHelper(environmentVariables, variables, feedUri, username, password, DockerHubRegistry));
            } 
            strategy.Execute(() => PerformLogin(username, password, feedHost, environmentVariables));

            const string cachedWorkerToolsShortLink = "https://g.octopushq.com/CachedWorkerToolsImages";
            var imageNotCachedMessage =
                "The docker image '{0}' may not be cached." + " Please note images that have not been cached may take longer to be acquired than expected." + " Your deployment will begin as soon as all images have been pulled." + $" Please see {cachedWorkerToolsShortLink} for more information on cached worker-tools image versions.";

            if (!IsImageCached(fullImageName))
            {
                log.InfoFormat(imageNotCachedMessage, fullImageName);
            }

            strategy.Execute(() => PerformPull(fullImageName, environmentVariables));

            var (hash, size) = GetImageDetails(fullImageName);
            
            // Cleanup credential helper files if used
            if (useCredentialHelper)
            {
                dockerCredentialHelper.CleanupCredentialHelper(environmentVariables);
            }

            if (fileSystem.DirectoryExists(DockerConfigFolder))
                fileSystem.DeleteDirectory(DockerConfigFolder);
            
            return new PackagePhysicalFileMetadata(new PackageFileNameMetadata(packageId, version, version, ""), string.Empty, hash, size);
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

        void PerformLogin(string? username, string? password, string feed, Dictionary<string, string> dictionary)
        {
            var envVars = new Dictionary<string, string>(dictionary);
            envVars["DockerUsername"] = username;
            envVars["DockerPassword"] = password;
            envVars["FeedUri"] = feed;
            
            var result = ExecuteScript("DockerLogin", envVars);
            if (result == null)
                throw new CommandException("Null result attempting to log in Docker registry");
            if (result.ExitCode != 0)
            {
                if (useCredentialHelper && result.Errors != null && result.Output.Contains("Error saving credentials"))
                {
                    log.Verbose("Docker login failed due to credential helper error, retrying without credential helper");
                    dockerCredentialHelper.CleanupCredentialHelper(environmentVariables);
                    PerformLogin(username, password, feed, dictionary);
                    return;
                }
                throw new CommandException("Unable to log in Docker registry");
            }
        }

        bool IsImageCached(string fullImageName)
        {
            var cachedDigests = GetCachedImageDigests();
            var selectedDigests = GetImageDigests(fullImageName);

            // If there are errors in the above steps, we treat the image as being cached and do not log image-not-cached
            if (cachedDigests == null || selectedDigests == null)
            {
                return true;
            }

            return cachedDigests.Intersect(selectedDigests).Any();
        }

        void PerformPull(string fullImageName, Dictionary<string, string> dictionary)
        {
            var envVars = new Dictionary<string, string>(dictionary);
            envVars["Image"] = fullImageName;
            
            var result = ExecuteScript("DockerPull", envVars);
            if (result == null)
                throw new CommandException("Null result attempting to pull Docker image");
            if (result.ExitCode != 0)
                throw new CommandException("Unable to pull Docker image");
        }

        CommandResult ExecuteScript(string scriptName, Dictionary<string, string?> envVars)
        {
            var file = ScriptExtractor.GetScript(fileSystem, scriptName, "Octopus.");
            using (new TemporaryFile(file))
            {
                var clone = variables.Clone();
                foreach (var keyValuePair in envVars)
                {
                    clone[keyValuePair.Key] = keyValuePair.Value;
                }

                return scriptEngine.Execute(new Script(file), clone, commandLineRunner, environmentVariables);
            }
        }

        (string hash, long size) GetImageDetails(string fullImageName)
        {
            var details = "";
            var result2 = SilentProcessRunner.ExecuteCommand("docker",
                                                             "inspect --format=\"{{.Id}} {{.Size}}\" " + fullImageName,
                                                             ".",
                                                             environmentVariables,
                                                             (stdout) => { details = stdout; },
                                                             log.Error);
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
                log.Verbose($"Unable to parse image size. ({parts[0]})");
            }

            return (hash, size);
        }

        IEnumerable<string>? GetCachedImageDigests()
        {
            var output = "";
            var result = SilentProcessRunner.ExecuteCommand("docker",
                                                            "image ls --format=\"{{.ID}}\" --no-trunc",
                                                            ".",
                                                            environmentVariables,
                                                            (stdout) => { output += stdout + " "; },
                                                            (error) => { });
            return result.ExitCode == 0
                ? output.Split(' ').Select(digest => digest.Trim())
                : null;
        }

        IEnumerable<string>? GetImageDigests(string fullImageName)
        {
            var output = "";
            var result = SilentProcessRunner.ExecuteCommand("docker",
                                                            $"manifest inspect --verbose {fullImageName}",
                                                            ".",
                                                            environmentVariables,
                                                            (stdout) => { output += stdout; },
                                                            (error) => { });

            if (result.ExitCode != 0)
            {
                return null;
            }

            if (!output.TrimStart().StartsWith("["))
            {
                output = $"[{output}]";
            }

            try
            {
                return JArray.Parse(output.ToLowerInvariant())
                             .Select(token => (string)token.SelectToken("schemav2manifest.config.digest"))
                             .ToList();
            }
            catch
            {
                return null;
            }
        }
    }
}
