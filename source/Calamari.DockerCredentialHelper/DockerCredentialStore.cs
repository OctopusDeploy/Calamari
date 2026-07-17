using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Calamari.DockerCredentialHelper
{
    public class DockerCredentialStore
    {
        const string CredentialsDirectory = "credentials";

        public void Store(string serverUrl, string username, string secret, string encryptionPassword, string dockerConfigPath)
        {
            var credentialsDir = Path.Combine(dockerConfigPath, CredentialsDirectory);
            Directory.CreateDirectory(credentialsDir);
            RestrictDirectoryToOwner(credentialsDir);

            var credential = new DockerCredential { Username = username, Secret = secret };
            var credentialJson = JsonSerializer.Serialize(credential, CredentialJsonContext.Default.DockerCredential);

            var encryptor = AesEncryption.ForScripts(encryptionPassword);
            var encryptedBytes = encryptor.Encrypt(credentialJson);

            var filePath = Path.Combine(credentialsDir, GetCredentialFileName(serverUrl));
            File.WriteAllBytes(filePath, encryptedBytes);
            RestrictFileToOwner(filePath);
        }

        static void RestrictDirectoryToOwner(string path) => RestrictToOwner(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        static void RestrictFileToOwner(string path) => RestrictToOwner(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        // The credential files only contain ciphertext, but restrict them to the owner anyway as
        // defense-in-depth (dir 0700, file 0600). No-op on Windows, which has no Unix file modes.
        static void RestrictToOwner(string path, UnixFileMode mode)
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, mode);
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
                return JsonSerializer.Deserialize(credentialJson, CredentialJsonContext.Default.DockerCredential);
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
}
