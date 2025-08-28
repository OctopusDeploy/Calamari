using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Calamari.Integration.Packages.Download
{
    public class DockerCredentialHelper
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        const string CredentialsDirectory = "credentials";

        public DockerCredentialHelper(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }
        
        public void StoreCredentials(string serverUrl, string username, string password, string encryptionPassword, string dockerConfigPath)
        {
            var credentialsDir = Path.Combine(dockerConfigPath, CredentialsDirectory);
            Directory.CreateDirectory(credentialsDir);
            
            var credential = new DockerCredential
            {
                Username = username,
                Secret = password
            };
            
            var credentialJson = JsonConvert.SerializeObject(credential);
            var encryptor = AesEncryption.ForScripts(encryptionPassword);
            var encryptedBytes = encryptor.Encrypt(credentialJson);
            
            var fileName = GetCredentialFileName(serverUrl);
            var filePath = Path.Combine(credentialsDir, fileName);
            
            File.WriteAllBytes(filePath, encryptedBytes);
            log.Verbose($"Stored encrypted credentials for {serverUrl}");
        }
        
        public DockerCredential? GetCredentials(string serverUrl, string encryptionPassword, string dockerConfigPath)
        {
            var fileName = GetCredentialFileName(serverUrl);
            var filePath = Path.Combine(dockerConfigPath, CredentialsDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                log.Verbose($"No stored credentials found for {serverUrl}");
                return null;
            }
            
            try
            {
                var encryptedBytes = File.ReadAllBytes(filePath);
                var encryptor = AesEncryption.ForScripts(encryptionPassword);
                var credentialJson = encryptor.Decrypt(encryptedBytes);
                
                var credential = JsonConvert.DeserializeObject<DockerCredential>(credentialJson);
                log.Verbose($"Retrieved credentials for {serverUrl}");
                return credential;
            }
            catch (Exception ex)
            {
                log.Verbose($"Failed to decrypt credentials for {serverUrl}: {ex.Message}");
                return null;
            }
        }
        
        public void EraseCredentials(string serverUrl, string dockerConfigPath)
        {
            var fileName = GetCredentialFileName(serverUrl);
            var filePath = Path.Combine(dockerConfigPath, CredentialsDirectory, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                log.Verbose($"Erased credentials for {serverUrl}");
            }
        }
        
        public string CreateDockerConfig(string dockerConfigPath, Dictionary<string, string> credHelpers)
        {
            var config = new DockerConfig
            {
                CredHelpers = credHelpers
            };
            
            var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            var configFilePath = Path.Combine(dockerConfigPath, "config.json");
            
            File.WriteAllText(configFilePath, configJson);
            return configFilePath;
        }
        
        public void CleanupCredentials(string dockerConfigPath)
        {
            var credentialsDir = Path.Combine(dockerConfigPath, CredentialsDirectory);
            if (Directory.Exists(credentialsDir))
            {
                try
                {
                    Directory.Delete(credentialsDir, recursive: true);
                    log.Verbose("Cleaned up Docker credential files");
                }
                catch (Exception ex)
                {
                    log.Verbose($"Failed to cleanup credential files: {ex.Message}");
                }
            }
        }
        
        static string GetCredentialFileName(string serverUrl)
        {
            var serverBytes = Encoding.UTF8.GetBytes(serverUrl);
            var base64Server = Convert.ToBase64String(serverBytes)
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "");
            return $"{base64Server}.cred";
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
                
                // Get the encryption password from sensitive variables
                var encryptionPassword = variables.Get("Octopus.Action.Package.DownloadOnTentacle") ?? 
                                        variables.Get("SensitiveVariablesPassword") ?? 
                                        "DefaultFallbackPassword";
                
                // Deploy credential helper scripts
                DeployCredentialHelperScript(environmentVariables, variables);
                
                // Store credentials using the helper
                var serverUrl = GetServerUrlForCredentialHelper(feedUri, dockerHubRegistry);
                StoreCredentials(serverUrl, username, password, encryptionPassword, dockerConfigPath);
                
                // Create Docker config with credential helper configuration
                var credHelpers = new Dictionary<string, string>();
                if (feedUri.Host.Equals(dockerHubRegistry))
                {
                    credHelpers["index.docker.io"] = "octopus";
                    credHelpers["docker.io"] = "octopus";
                    credHelpers["registry-1.docker.io"] = "octopus";
                }
                else
                {
                    credHelpers[feedUri.Host] = "octopus";
                    if (feedUri.Port != -1 && feedUri.Port != 80 && feedUri.Port != 443)
                    {
                        credHelpers[$"{feedUri.Host}:{feedUri.Port}"] = "octopus";
                    }
                }
                
                CreateDockerConfig(dockerConfigPath, credHelpers);
                log.Verbose($"Configured Docker credential helper for {serverUrl}");
                return true;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to setup credential helper: {ex.Message}");
                return false;
            }
        }

        static string GetCalamariExecutablePath()
        {
            // First try to get the entry assembly (works in production)
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                var entryLocation = entryAssembly.Location;
                var entryName = Path.GetFileNameWithoutExtension(entryLocation);
                
                // If the entry assembly is Calamari itself, use it
                if (entryName.Equals("Calamari", StringComparison.OrdinalIgnoreCase))
                {
                    return entryLocation;
                }
            }
            
            // Fallback for test scenarios: look for Calamari executable in the same directory as this assembly
            var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var currentDirectory = Path.GetDirectoryName(currentAssembly.Location);
            
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                // Try different possible names
                var possibleNames = new[] { "Calamari.exe", "Calamari" };
                
                foreach (var name in possibleNames)
                {
                    var candidatePath = Path.Combine(currentDirectory, name);
                    if (File.Exists(candidatePath))
                    {
                        return candidatePath;
                    }
                }
            }
            
            // Last resort: use "Calamari" and hope it's in PATH
            return "Calamari";
        }

        void DeployCredentialHelperScript(Dictionary<string, string> environmentVariables, IVariables variables)
        {
            var dockerConfigPath = environmentVariables["DOCKER_CONFIG"];
            var scriptName = "docker-credential-octopus";
            var helperScript = ScriptExtractor.GetScript(fileSystem, scriptName);
            
            // Make the script executable on Unix systems
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                var result = SilentProcessRunner.ExecuteCommand("chmod", $"+x {helperScript}", ".", new Dictionary<string, string>(), _ => { }, _ => { });
                if (result.ExitCode != 0)
                {
                    log.Verbose($"Failed to make credential helper script executable: {result.ExitCode}");
                }
            }
            
            // Add the script directory to PATH for Docker to find the helper
            var scriptDir = Path.GetDirectoryName(Path.GetFullPath(helperScript));
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            var pathSeparator = CalamariEnvironment.IsRunningOnWindows ? ";" : ":";
            
            if (!currentPath.Split(pathSeparator.ToCharArray()).Contains(scriptDir))
            {
                environmentVariables["PATH"] = $"{scriptDir}{pathSeparator}{currentPath}";
            }
            
            // Set environment variables for the credential helper
            var encryptionPassword = variables.Get("Octopus.Action.Package.DownloadOnTentacle") ?? 
                                    variables.Get("SensitiveVariablesPassword") ?? 
                                    "DefaultFallbackPassword";
            
            // Pass the Calamari executable path to the credential helper script
            var calamariExecutable = GetCalamariExecutablePath();
            if (!string.IsNullOrEmpty(calamariExecutable))
            {
                environmentVariables["OCTOPUS_CALAMARI_EXECUTABLE"] = calamariExecutable;
            }
            
            environmentVariables["OCTOPUS_CREDENTIAL_PASSWORD"] = encryptionPassword;
        }

        public void CleanupCredentialHelper(Dictionary<string, string> environmentVariables)
        {
            try
            {
                var dockerConfigPath = environmentVariables["DOCKER_CONFIG"];
                CleanupCredentials(dockerConfigPath);
            }
            catch (Exception ex)
            {
                log.Verbose($"Failed to cleanup credential helper files: {ex.Message}");
            }
        }
        
        public static string GetServerUrlForCredentialHelper(Uri feedUri, string dockerHubRegistry)
        {
            // Docker credential helpers expect specific server URLs
            if (feedUri.Host.Equals(dockerHubRegistry))
            {
                return "https://index.docker.io/v1/";
            }
            
            return feedUri.GetLeftPart(UriPartial.Authority);
        }

    }
    
    public class DockerCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }
    
    public class DockerConfig
    {
        [JsonProperty("credHelpers")]
        public Dictionary<string, string> CredHelpers { get; set; } = new Dictionary<string, string>();
    }
}
