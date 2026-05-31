using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Docker;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Integration.Packages.Download
{
    public class DockerCredentialHelper
    {
        // Docker resolves credential helpers by the binary name `docker-credential-<name>`.
        const string CredentialHelperName = "octopus";

        readonly ILog log;
        readonly DockerCredentialStore store = new DockerCredentialStore();

        public DockerCredentialHelper(ILog log)
        {
            this.log = log;
        }

        public bool SetupCredentialHelper(Dictionary<string, string> environmentVariables,
                                          IVariables variables,
                                          Uri feedUri,
                                          string username,
                                          string password,
                                          string dockerHubRegistry)
        {
            try
            {
                var dockerConfigPath = environmentVariables["DOCKER_CONFIG"];
                Directory.CreateDirectory(dockerConfigPath);

                var encryptionPassword = GetEncryptionPassword(variables);

                // docker-credential-octopus is published alongside Calamari, so it lives in the app base directory.
                AddDirectoryToPath(environmentVariables, AppContext.BaseDirectory);
                environmentVariables["OCTOPUS_CREDENTIAL_PASSWORD"] = encryptionPassword;

                var serverUrl = GetServerUrlForCredentialHelper(feedUri, dockerHubRegistry);
                store.Store(serverUrl, username, password, encryptionPassword, dockerConfigPath);

                CreateDockerConfig(dockerConfigPath, BuildCredHelpers(feedUri, dockerHubRegistry));

                log.Verbose($"Configured Docker credential helper for {serverUrl}");
                return true;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to setup credential helper: {ex.Message}");
                return false;
            }
        }

        public void CleanupCredentialHelper(Dictionary<string, string> environmentVariables)
        {
            try
            {
                var dockerConfigPath = environmentVariables["DOCKER_CONFIG"];

                var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
                if (Directory.Exists(credentialsDir))
                    Directory.Delete(credentialsDir, recursive: true);

                var configFilePath = Path.Combine(dockerConfigPath, "config.json");
                if (File.Exists(configFilePath))
                    File.Delete(configFilePath);
            }
            catch (Exception ex)
            {
                log.Verbose($"Failed to cleanup credential helper files: {ex.Message}");
            }
        }

        public string CreateDockerConfig(string dockerConfigPath, Dictionary<string, string> credHelpers)
        {
            var config = new DockerConfig { CredHelpers = credHelpers };
            var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            var configFilePath = Path.Combine(dockerConfigPath, "config.json");
            File.WriteAllText(configFilePath, configJson);
            return configFilePath;
        }

        public static string GetServerUrlForCredentialHelper(Uri feedUri, string dockerHubRegistry)
        {
            if (feedUri.Host.Equals(dockerHubRegistry))
                return "https://index.docker.io/v1/";

            return feedUri.GetLeftPart(UriPartial.Authority);
        }

        static string GetEncryptionPassword(IVariables variables)
        {
            // NOTE: carried over from PR #1542 — confirm the correct sensitive-variable password
            // source during review (see spec "Open implementation notes").
            return variables.Get("Octopus.Action.Package.DownloadOnTentacle")
                   ?? variables.Get("SensitiveVariablesPassword")
                   ?? "DefaultFallbackPassword";
        }

        static Dictionary<string, string> BuildCredHelpers(Uri feedUri, string dockerHubRegistry)
        {
            var credHelpers = new Dictionary<string, string>();
            if (feedUri.Host.Equals(dockerHubRegistry))
            {
                credHelpers["index.docker.io"] = CredentialHelperName;
                credHelpers["docker.io"] = CredentialHelperName;
                credHelpers["registry-1.docker.io"] = CredentialHelperName;
                credHelpers["https://index.docker.io/v1/"] = CredentialHelperName;
            }
            else
            {
                credHelpers[feedUri.Host] = CredentialHelperName;
                if (feedUri.Port != -1 && feedUri.Port != 80 && feedUri.Port != 443)
                    credHelpers[$"{feedUri.Host}:{feedUri.Port}"] = CredentialHelperName;
            }

            return credHelpers;
        }

        static void AddDirectoryToPath(Dictionary<string, string> environmentVariables, string directory)
        {
            var pathSeparator = CalamariEnvironment.IsRunningOnWindows ? ";" : ":";
            var currentPath = environmentVariables.TryGetValue("PATH", out var existing)
                ? existing
                : Environment.GetEnvironmentVariable("PATH") ?? "";

            if (!currentPath.Split(pathSeparator.ToCharArray()).Contains(directory))
                environmentVariables["PATH"] = $"{directory}{pathSeparator}{currentPath}";
        }
    }

    public class DockerConfig
    {
        [JsonProperty("credHelpers")]
        public Dictionary<string, string> CredHelpers { get; set; } = new Dictionary<string, string>();
    }
}
