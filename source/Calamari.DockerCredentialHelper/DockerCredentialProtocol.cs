using System.IO;
using System.Text.Json;
using Calamari.Common.Features.Docker;

namespace Calamari.DockerCredentialHelper
{
    public class DockerCredentialProtocol
    {
        readonly DockerCredentialStore store;

        public DockerCredentialProtocol(DockerCredentialStore store)
        {
            this.store = store;
        }

        public int Run(string operation, TextReader input, TextWriter output, TextWriter error, string encryptionPassword, string dockerConfigPath)
        {
            switch (operation.ToLowerInvariant())
            {
                case "store":
                    return Store(input, error, encryptionPassword, dockerConfigPath);
                case "get":
                    return Get(input, output, error, encryptionPassword, dockerConfigPath);
                case "erase":
                    return Erase(input, dockerConfigPath);
                default:
                    error.WriteLine($"Invalid operation: {operation}. Valid operations are: store, get, erase");
                    return 1;
            }
        }

        int Store(TextReader input, TextWriter error, string encryptionPassword, string dockerConfigPath)
        {
            var request = JsonSerializer.Deserialize<StoreRequest>(input.ReadToEnd());
            if (request == null || string.IsNullOrEmpty(request.ServerURL))
            {
                error.WriteLine("Invalid store request");
                return 1;
            }

            store.Store(request.ServerURL, request.Username, request.Secret, encryptionPassword, dockerConfigPath);
            return 0;
        }

        int Get(TextReader input, TextWriter output, TextWriter error, string encryptionPassword, string dockerConfigPath)
        {
            var serverUrl = input.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(serverUrl))
            {
                error.WriteLine("No server URL provided");
                return 1;
            }

            var credential = store.Get(serverUrl, encryptionPassword, dockerConfigPath);
            if (credential == null)
            {
                error.WriteLine("credentials not found in native keychain");
                return 1;
            }

            var response = new GetResponse { ServerURL = serverUrl, Username = credential.Username, Secret = credential.Secret };
            output.WriteLine(JsonSerializer.Serialize(response));
            return 0;
        }

        int Erase(TextReader input, string dockerConfigPath)
        {
            var serverUrl = input.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(serverUrl))
                store.Erase(serverUrl, dockerConfigPath);
            return 0;
        }

        class StoreRequest
        {
            public string ServerURL { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Secret { get; set; } = string.Empty;
        }

        class GetResponse
        {
            public string ServerURL { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Secret { get; set; } = string.Empty;
        }
    }
}
