using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Common.Features.Docker
{
    public class DockerCredentialStore
    {
        const string CredentialsDirectory = "credentials";

        public void Store(string serverUrl, string username, string secret, string encryptionPassword, string dockerConfigPath)
        {
            var credentialsDir = Path.Combine(dockerConfigPath, CredentialsDirectory);
            Directory.CreateDirectory(credentialsDir);

            var credential = new DockerCredential { Username = username, Secret = secret };
            var credentialJson = JsonSerializer.Serialize(credential);

            var encryptor = AesEncryption.ForScripts(encryptionPassword);
            var encryptedBytes = encryptor.Encrypt(credentialJson);

            var filePath = Path.Combine(credentialsDir, GetCredentialFileName(serverUrl));
            File.WriteAllBytes(filePath, encryptedBytes);
        }

        public DockerCredential? Get(string serverUrl, string encryptionPassword, string dockerConfigPath)
        {
            var filePath = Path.Combine(dockerConfigPath, CredentialsDirectory, GetCredentialFileName(serverUrl));
            if (!File.Exists(filePath))
                return null;

            try
            {
                var encryptedBytes = File.ReadAllBytes(filePath);
                var encryptor = AesEncryption.ForScripts(encryptionPassword);
                var credentialJson = encryptor.Decrypt(encryptedBytes);
                return JsonSerializer.Deserialize<DockerCredential>(credentialJson);
            }
            // A missing, corrupt, or wrong-password credential is treated as "not found".
            catch (Exception)
            {
                return null;
            }
        }

        public void Erase(string serverUrl, string dockerConfigPath)
        {
            var filePath = Path.Combine(dockerConfigPath, CredentialsDirectory, GetCredentialFileName(serverUrl));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public static string GetCredentialFileName(string serverUrl)
        {
            var serverBytes = Encoding.UTF8.GetBytes(serverUrl);
            var base64Server = Convert.ToBase64String(serverBytes)
                                      .Replace("/", "_")
                                      .Replace("+", "-")
                                      .Replace("=", "");
            return $"{base64Server}.cred";
        }
    }

    public class DockerCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }
}
