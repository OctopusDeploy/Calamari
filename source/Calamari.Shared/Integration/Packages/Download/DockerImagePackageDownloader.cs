using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Common.Commands;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    // Note about moving this class: GetScript method uses the namespace of this class as part of the
    // get Embedded Resource to find the DockerLogin and DockerPull scripts. If you move this file, be sure look at that method
    // and make sure it can still find the scripts
    public class DockerImagePackageDownloader : IPackageDownloader
    {
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly IVariables variables;
        readonly ILog log;
        const string DockerHubRegistry = "index.docker.io";

        // Ensures that any credential details are only available for the duration of the acquisition
        readonly Dictionary<string, string> environmentVariables = new Dictionary<string, string>()
        {
            {
                "DOCKER_CONFIG", "./octo-docker-configs"
            }
        };

        public DockerImagePackageDownloader(IScriptEngine scriptEngine,
                                            ICalamariFileSystem fileSystem,
                                            ICommandLineRunner commandLineRunner,
                                            IVariables variables,
                                            ILog log)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
            this.log = log;
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
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get("Jwt"));

            if (variables.Get("FeedType") == FeedType.AwsElasticContainerRegistry.ToString())
            {
                if (usingOidc)
                {
                    try
                    {
                        var oidcCredentials = GetAwsOidcCredentials().GetAwaiter().GetResult();
                        username = oidcCredentials.Username;
                        password = oidcCredentials.Password;
                        feedUri = new Uri(oidcCredentials.RegistryUri);

                        log.Verbose("Successfully obtained ECR credentials using OIDC token");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to get ECR credentials using OIDC: {ex.Message}");
                        throw;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(username))
                {
                    var accessKeyCreds = GetAwsAccessKeyCredentials(username, password);
                    username = accessKeyCreds.Username;
                    password = accessKeyCreds.Password;
                    feedUri = new Uri(accessKeyCreds.RegistryUri);
                }
            }
            
            //Always try re-pull image, docker engine can take care of the rest
            var fullImageName = GetFullImageName(packageId, version, feedUri);

            var feedHost = GetFeedHost(feedUri);

            var strategy = PackageDownloaderRetryUtils.CreateRetryStrategy<CommandException>(maxDownloadAttempts, downloadAttemptBackoff, log);
            strategy.Execute(() => PerformLogin(username, password, feedHost));

            const string cachedWorkerToolsShortLink = "https://g.octopushq.com/CachedWorkerToolsImages";
            var imageNotCachedMessage =
                "The docker image '{0}' may not be cached." + " Please note images that have not been cached may take longer to be acquired than expected." + " Your deployment will begin as soon as all images have been pulled." + $" Please see {cachedWorkerToolsShortLink} for more information on cached worker-tools image versions.";

            if (!IsImageCached(fullImageName))
            {
                log.InfoFormat(imageNotCachedMessage, fullImageName);
            }

            strategy.Execute(() => PerformPull(fullImageName));

            var (hash, size) = GetImageDetails(fullImageName);
            return new PackagePhysicalFileMetadata(new PackageFileNameMetadata(packageId, version, version, ""), string.Empty, hash, size);
        }

        (string Username, string Password, string RegistryUri) GetAwsAccessKeyCredentials(string accessKey, string? secretKey)
        {
            var region = variables.Get("Region");
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var client = new AmazonECRClient(credentials, regionEndpoint);
            try
            {
                var token = client.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest()).GetAwaiter().GetResult();
                var authToken = token.AuthorizationData.FirstOrDefault();
                if (authToken == null)
                {
                    throw new Exception("No AuthToken found");
                }

                var creds = DecodeCredentials(authToken);
                return (creds.Username, creds.Password, authToken.ProxyEndpoint);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Unable to retrieve AWS Authorization token:\r\n\t{ex.Message}");
            }
        }
        
        (string Username, string Password) DecodeCredentials(AuthorizationData authToken)
        {
            try
            {
                var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(authToken.AuthorizationToken));
                var parts = decodedToken.Split(':');
                if (parts.Length != 2)
                {
                    throw new AuthenticationException("Token returned by AWS is in an unexpected format");
                }

                return (parts[0], parts[1]);
            }
            catch (Exception)
            {
                throw new AuthenticationException("Token returned by AWS is in an unexpected format");
            }
        }

        // TODO: move this somewhere else later
        async Task<(string Username, string Password, string RegistryUri)> GetAwsOidcCredentials()
        {
            try
            {
                var jwt = variables.Get("Jwt");
                var roleArn = variables.Get("RoleArn");
                var region = variables.Get("Region");
                var sessionDuration = variables.Get("SessionDuration");
                
                var client = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
                var assumeRoleWithWebIdentityResponse = await client.AssumeRoleWithWebIdentityAsync(new AssumeRoleWithWebIdentityRequest
                {
                    RoleArn = roleArn,
                    DurationSeconds = int.TryParse(sessionDuration, out var seconds) ? seconds : 3600,
                    RoleSessionName = "OctopusAwsAuthentication",
                    WebIdentityToken = jwt
                });
                var credentials = new SessionAWSCredentials(assumeRoleWithWebIdentityResponse.Credentials.AccessKeyId,
                                                            assumeRoleWithWebIdentityResponse.Credentials.SecretAccessKey,
                                                            assumeRoleWithWebIdentityResponse.Credentials.SessionToken);
            
                var regionEndpoint = RegionEndpoint.GetBySystemName(region);
                var ecrClient = new AmazonECRClient(credentials, regionEndpoint);
                var tokenResponse = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        
                var authToken = tokenResponse.AuthorizationData.FirstOrDefault();
                if (authToken == null)
                {
                    throw new Exception("No AuthToken found");
                }
                var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(authToken.AuthorizationToken));
                var parts = decodedToken.Split(':');
        
                if (parts.Length != 2)
                {
                    throw new Exception("Token returned by AWS is in an unexpected format");
                }
        
                return (parts[0], parts[1], authToken.ProxyEndpoint);
            }
            catch (Exception ex)
            {
                // catch the exception and fallback to returning false
                throw new Exception("AWS-LOGIN-ERROR-0005.1: Failed to verify OIDC credentials. "
                                    + $"Error: {ex}");
            }
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

        void PerformLogin(string? username, string? password, string feed)
        {
            var result = ExecuteScript("DockerLogin",
                                       new Dictionary<string, string?>
                                       {
                                           ["DockerUsername"] = username,
                                           ["DockerPassword"] = password,
                                           ["FeedUri"] = feed
                                       });
            if (result == null)
                throw new CommandException("Null result attempting to log in Docker registry");
            if (result.ExitCode != 0)
                throw new CommandException("Unable to log in Docker registry");
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

        void PerformPull(string fullImageName)
        {
            var result = ExecuteScript("DockerPull",
                                       new Dictionary<string, string?>
                                       {
                                           ["Image"] = fullImageName
                                       });
            if (result == null)
                throw new CommandException("Null result attempting to pull Docker image");
            if (result.ExitCode != 0)
                throw new CommandException("Unable to pull Docker image");
        }

        CommandResult ExecuteScript(string scriptName, Dictionary<string, string?> envVars)
        {
            var file = GetScript(scriptName);
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

        string GetScript(string scriptName)
        {
            var syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();

            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = $"{scriptName}.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = $"{scriptName}.ps1";
                    break;
                default:
                    throw new InvalidOperationException("No kubernetes context wrapper exists for " + syntax);
            }

            var scriptFile = Path.Combine(".", $"Octopus.{contextFile}");
            var contextScript = new AssemblyEmbeddedResources().GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"{typeof(DockerImagePackageDownloader).Namespace}.Scripts.{contextFile}");
            fileSystem.OverwriteFile(scriptFile, contextScript);
            return scriptFile;
        }
    }
}