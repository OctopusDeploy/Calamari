using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download;
using Newtonsoft.Json;

namespace Calamari.Commands
{
    [Command("docker-credential", Description = "Docker credential helper operations for secure credential storage")]
    public class DockerCredentialCommand : Command, IWantCustomHandlingOfDeferredLogs
    {
        readonly ILog log;
        string operation = string.Empty;
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        public DockerCredentialCommand(ILog log)
        {
            this.log = log;

            Options.Add("operation=", "The credential operation to perform (store, get, erase)", v => operation = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (string.IsNullOrEmpty(operation))
            {
                log.Error("Operation parameter is required (store, get, erase)");
                FlushLogs();
                return 1;
            }

            var encryptionPassword = Environment.GetEnvironmentVariable("OCTOPUS_CREDENTIAL_PASSWORD");
            var dockerConfigPath = Environment.GetEnvironmentVariable("DOCKER_CONFIG");

            if (string.IsNullOrEmpty(encryptionPassword))
            {
                log.Error("OCTOPUS_CREDENTIAL_PASSWORD environment variable not set");
                FlushLogs();
                return 1;
            }

            if (string.IsNullOrEmpty(dockerConfigPath))
            {
                log.Error("DOCKER_CONFIG environment variable not set");
                FlushLogs();
                return 1;
            }

            var dockerCredentialHelper = new DockerCredentialHelper(fileSystem, log);

            try
            {
                switch (operation.ToLower())
                {
                    case "store":
                        return StoreCredential(dockerCredentialHelper, encryptionPassword, dockerConfigPath);
                    case "get":
                        return GetCredential(dockerCredentialHelper, encryptionPassword, dockerConfigPath);
                    case "erase":
                        return EraseCredential(dockerCredentialHelper, dockerConfigPath);
                    default:
                        log.Error($"Invalid operation: {operation}. Valid operations are: store, get, erase");
                        FlushLogs();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Docker credential operation failed: {ex.Message}");
                FlushLogs();
                return 1;
            }
        }

        void FlushLogs()
        {
            if (log is DeferredLogger logger) 
                logger.FlushDeferredLogs();
        }

        int StoreCredential(DockerCredentialHelper dockerCredentialHelper, string encryptionPassword, string dockerConfigPath)
        {
            var inputJson = Console.In.ReadToEnd();
            var credentialRequest = JsonConvert.DeserializeObject<dynamic>(inputJson);

            var serverUrl = (string)credentialRequest.ServerURL;
            var username = (string)credentialRequest.Username;
            var secret = (string)credentialRequest.Secret;

            dockerCredentialHelper.StoreCredentials(serverUrl, username, secret, encryptionPassword, dockerConfigPath);
            return 0;
        }

        int GetCredential(DockerCredentialHelper dockerCredentialHelper, string encryptionPassword, string dockerConfigPath)
        {
            var serverUrl = Console.ReadLine();
            var credential = dockerCredentialHelper.GetCredentials(serverUrl, encryptionPassword, dockerConfigPath);

            if (credential == null)
            {
                Console.Error.WriteLine("credentials not found in native keychain");
                return 1;
            }

            var response = new { ServerURL = serverUrl, Username = credential.Username, Secret = credential.Secret };
            Console.WriteLine(JsonConvert.SerializeObject(response));
            return 0;
        }

        int EraseCredential(DockerCredentialHelper dockerCredentialHelper, string dockerConfigPath)
        {
            var serverUrl = Console.ReadLine();
            dockerCredentialHelper.EraseCredentials(serverUrl, dockerConfigPath);
            return 0;
        }
    }
}
