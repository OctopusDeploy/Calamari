using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Integration.Packages.Download;
using Calamari.Testing.Requirements;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class DockerCredentialScriptsFixture
    {
        string tempDirectory;
        string dockerConfigPath;
        string powershellScript;
        string bashScript;
        string calamariExecutable;
        
        const string TestEncryptionPassword = "TestPassword123!";
        const string TestServerUrl = "https://index.docker.io/v1/";
        const string TestUsername = "testuser";
        const string TestPassword = "testpass";

        [SetUp]
        public void Setup()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            dockerConfigPath = Path.Combine(tempDirectory, "docker-config");
            Directory.CreateDirectory(dockerConfigPath);

            // Create the credential helper scripts in temp directory
            CreateCredentialHelperScripts();
            
            // Find or create a mock Calamari executable
            SetupCalamariExecutable();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        void CreateCredentialHelperScripts()
        {
            // Use the same embedded resources that the real DockerImagePackageDownloader uses
            var embeddedResources = new AssemblyEmbeddedResources();
            var dockerImagePackageDownloaderAssembly = typeof(DockerImagePackageDownloader).Assembly;
            var scriptsNamespace = $"{typeof(DockerImagePackageDownloader).Namespace}.Scripts";
            
            // Create PowerShell script from embedded resource
            powershellScript = Path.Combine(tempDirectory, "docker-credential-octopus.ps1");
            var powershellContent = embeddedResources.GetEmbeddedResourceText(dockerImagePackageDownloaderAssembly, $"{scriptsNamespace}.docker-credential-octopus.ps1");
            File.WriteAllText(powershellScript, powershellContent);

            // Create Bash script from embedded resource
            bashScript = Path.Combine(tempDirectory, "docker-credential-octopus.sh");
            var bashContent = embeddedResources.GetEmbeddedResourceText(dockerImagePackageDownloaderAssembly, $"{scriptsNamespace}.docker-credential-octopus.sh");
            // Ensure Unix line endings for bash script to avoid issues on CI/Unix systems
            var normalizedBashContent = bashContent.Replace("\r\n", "\n").Replace("\r", "\n");
            File.WriteAllText(bashScript, normalizedBashContent);
            
            // Make bash script executable on Unix systems
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                SilentProcessRunner.ExecuteCommand("chmod", $"+x {bashScript}", ".", new Dictionary<string, string>(), _ => { }, _ => { });
            }
        }

        void SetupCalamariExecutable()
        {
            // Use the real Calamari executable for true integration testing
            var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var testBinDirectory = Path.GetDirectoryName(testAssemblyLocation);
            
            // Look for Calamari executable in multiple potential locations
            var searchDirectories = new[]
            {
                testBinDirectory, // Same directory as test assembly
                Path.Combine(testBinDirectory, "..", "..", "..", "..", "bin", "Debug", "net462"), // Relative path to main bin
                Path.Combine(testBinDirectory, "..", "..", "..", "..", "..", "bin", "Debug", "net462"), // Another potential path
                Path.Combine(testBinDirectory, "..", "Binaries"), // CI build path
                Environment.GetEnvironmentVariable("CALAMARI_EXECUTABLE_PATH") // Allow override via env var
            }.Where(dir => !string.IsNullOrEmpty(dir));
            
            // On Unix systems it's "Calamari", on Windows it's "Calamari.exe"
            var executableNames = CalamariEnvironment.IsRunningOnWindows 
                ? new[] { "Calamari.exe", "Calamari" }
                : new[] { "Calamari", "Calamari.exe" };
            
            foreach (var searchDirectory in searchDirectories)
            {
                foreach (var executableName in executableNames)
                {
                    var candidatePath = Path.Combine(searchDirectory, executableName);
                    if (File.Exists(candidatePath))
                    {
                        calamariExecutable = Path.GetFullPath(candidatePath);
                        
                        // Make sure the executable has execute permissions on Unix systems
                        if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                        {
                            SilentProcessRunner.ExecuteCommand("chmod", $"+x \"{calamariExecutable}\"", ".", new Dictionary<string, string>(), _ => { }, _ => { });
                        }
                        return;
                    }
                }
            }
            
            throw new InvalidOperationException($"Could not find Calamari executable. Searched in: {string.Join(", ", searchDirectories)}. Tried names: {string.Join(", ", executableNames)}. Make sure the project is built.");
        }


        [Test]
        [WindowsTest]
        public void PowerShellScript_WithStoreOperation_CallsCalamariCorrectly()
        {
            // Arrange
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });

            // Act
            var result = ExecutePowerShellScript("store", credentialJson);

            // Assert
            result.ExitCode.Should().Be(0, $"Script execution failed. Output: {result.Output}, Error: {result.Error}");
            
            // Verify credentials were actually stored by the real command
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.Exists(credentialsDir).Should().BeTrue($"Expected credentials directory '{credentialsDir}' to exist");
            var credFiles = Directory.Exists(credentialsDir) ? Directory.GetFiles(credentialsDir, "*.cred") : new string[0];
            credFiles.Should().HaveCount(1, $"Expected exactly 1 credential file in '{credentialsDir}', but found: {string.Join(", ", credFiles)}");
        }

        [Test]
        [WindowsTest]
        public void PowerShellScript_WithGetOperation_ReturnsCredentials()
        {
            // Arrange - First store credentials
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });
            ExecutePowerShellScript("store", credentialJson);

            // Act
            var result = ExecutePowerShellScript("get", TestServerUrl);

            // Assert
            result.ExitCode.Should().Be(0, $"Script execution failed. Output: {result.Output}, Error: {result.Error}");
            
            // Parse and verify the returned JSON credentials
            // Extract the JSON line from Calamari output (skip verbose logging)
            var outputLines = result.Output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var jsonLine = outputLines.FirstOrDefault(line => line.Trim().StartsWith("{"));
            jsonLine.Should().NotBeNull($"Expected JSON response from credential get operation. Full output: {result.Output}, Error: {result.Error}");
            
            dynamic responseJson;
            try 
            {
                responseJson = JsonConvert.DeserializeObject<dynamic>(jsonLine.Trim());
            }
            catch (Exception ex)
            {
                throw new AssertionFailedException($"Failed to parse JSON response '{jsonLine}'. Error: {ex.Message}. Full output: {result.Output}");
            }
            
            ((string)responseJson.Username).Should().Be(TestUsername, $"Username mismatch in response: {jsonLine}");
            ((string)responseJson.Secret).Should().Be(TestPassword, $"Password mismatch in response: {jsonLine}");
        }

        [Test]
        [WindowsTest]
        public void PowerShellScript_WithEraseOperation_CallsCalamariCorrectly()
        {
            // Arrange - First store credentials
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });
            var result = ExecutePowerShellScript("store", credentialJson);
            result.ExitCode.Should().Be(0, $"docker-credential store execution failed. Output: {result.Output}, Error: {result.Error}");

            
            // Verify credential exists before erase
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            var existingFiles = Directory.Exists(credentialsDir) ? Directory.GetFiles(credentialsDir, "*.cred") : Array.Empty<string>();
            existingFiles.Should().HaveCount(1, $"Expected exactly 1 credential file before erase in '{credentialsDir}', but found: {string.Join(", ", existingFiles)}");

            // Act
            result = ExecutePowerShellScript("erase", TestServerUrl);

            // Assert
            result.ExitCode.Should().Be(0, $"docker-credential erase execution failed. Output: {result.Output}, Error: {result.Error}");
            
            // Verify credential was actually erased
            var remainingFiles = Directory.Exists(credentialsDir) ? Directory.GetFiles(credentialsDir, "*.cred") : Array.Empty<string>();
            remainingFiles.Should().BeEmpty($"Expected no credential files after erase, but found: {string.Join(", ", remainingFiles)}");
        }

        [Test]
        [NonWindowsTest]
        public void BashScript_WithStoreOperation_CallsCalamariCorrectly()
        {
            // Arrange
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });

            // Act
            var result = ExecuteBashScript("store", credentialJson);

            // Assert
            result.ExitCode.Should().Be(0, $"Script execution failed. Output: {result.Output}, Error: {result.Error}");
            
            // Verify credentials were actually stored by the real command
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.Exists(credentialsDir).Should().BeTrue($"Expected credentials directory '{credentialsDir}' to exist");
            var credFiles = Directory.Exists(credentialsDir) ? Directory.GetFiles(credentialsDir, "*.cred") : new string[0];
            credFiles.Should().HaveCount(1, $"Expected exactly 1 credential file in '{credentialsDir}', but found: {string.Join(", ", credFiles)}");
        }

        [Test]
        [NonWindowsTest]
        public void BashScript_WithGetOperation_ReturnsCredentials()
        {
            // Arrange - First store credentials
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });
            ExecuteBashScript("store", credentialJson);

            // Act
            var result = ExecuteBashScript("get", TestServerUrl);

            // Assert
            result.ExitCode.Should().Be(0, $"Script execution failed. Output: {result.Output}, Error: {result.Error}");
            
            // Parse and verify the returned JSON credentials
            // Extract the JSON line from Calamari output (skip verbose logging)
            var outputLines = result.Output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var jsonLine = outputLines.FirstOrDefault(line => line.Trim().StartsWith("{"));
            jsonLine.Should().NotBeNull($"Expected JSON response from credential get operation. Full output: {result.Output}, Error: {result.Error}");
            
            dynamic responseJson;
            try 
            {
                responseJson = JsonConvert.DeserializeObject<dynamic>(jsonLine.Trim());
            }
            catch (Exception ex)
            {
                throw new AssertionFailedException($"Failed to parse JSON response '{jsonLine}'. Error: {ex.Message}. Full output: {result.Output}");
            }
            
            ((string)responseJson.Username).Should().Be(TestUsername, $"Username mismatch in response: {jsonLine}");
            ((string)responseJson.Secret).Should().Be(TestPassword, $"Password mismatch in response: {jsonLine}");
        }

        [Test]
        [NonWindowsTest]
        public void BashScript_WithEraseOperation_CallsCalamariCorrectly()
        {
            Console.WriteLine($"[TEST DEBUG] Starting BashScript_WithEraseOperation_CallsCalamariCorrectly");
            
            // Arrange - First store credentials
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });
            ExecuteBashScript("store", credentialJson);
            
            // Verify credential exists before erase
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            var existingFiles = Directory.Exists(credentialsDir) ? Directory.GetFiles(credentialsDir, "*.cred") : new string[0];
            existingFiles.Should().HaveCount(1, $"Expected exactly 1 credential file before erase in '{credentialsDir}', but found: {string.Join(", ", existingFiles)}");

            // Act
            var result = ExecuteBashScript("erase", TestServerUrl);

            // Assert
            result.ExitCode.Should().Be(0, $"Script execution failed. Output: {result.Output}, Error: {result.Error}");
            
            // Verify credential was actually erased
            var remainingFiles = Directory.Exists(credentialsDir) ? Directory.GetFiles(credentialsDir, "*.cred") : new string[0];
            remainingFiles.Should().BeEmpty($"Expected no credential files after erase, but found: {string.Join(", ", remainingFiles)}");
        }

        [Test]
        public void ScriptGeneration_CreatesValidPowerShellScript()
        {
            // Assert
            File.Exists(powershellScript).Should().BeTrue();
            var content = File.ReadAllText(powershellScript);
            
            content.Should().Contain("param(");
            content.Should().Contain("$Operation");
            content.Should().Contain("OCTOPUS_CALAMARI_EXECUTABLE");
            content.Should().Contain("docker-credential");
            content.Should().Contain("--operation=");
        }

        [Test]
        public void ScriptGeneration_CreatesValidBashScript()
        {
            // Assert
            File.Exists(bashScript).Should().BeTrue();
            var content = File.ReadAllText(bashScript);
            
            content.Should().StartWith("#!/bin/bash");
            content.Should().Contain("OPERATION=\"$1\"");
            content.Should().Contain("OCTOPUS_CALAMARI_EXECUTABLE");
            content.Should().Contain("docker-credential");
            content.Should().Contain("--operation=");
        }

        ScriptExecutionResult ExecutePowerShellScript(string operation, string input = null)
        {
            Console.WriteLine($"[TEST DEBUG] Executing PowerShell script: {powershellScript} with operation: {operation}");
            Console.WriteLine($"[TEST DEBUG] Calamari executable: {calamariExecutable}");
            Console.WriteLine($"[TEST DEBUG] Docker config path: {dockerConfigPath}");
            
            var environmentVariables = new Dictionary<string, string>
            {
                ["OCTOPUS_CALAMARI_EXECUTABLE"] = calamariExecutable,
                ["DOCKER_CONFIG"] = dockerConfigPath,
                ["OCTOPUS_CREDENTIAL_PASSWORD"] = TestEncryptionPassword
            };

            var result = ExecuteProcessWithInput("powershell.exe", 
                $"-ExecutionPolicy Bypass -File \"{powershellScript}\" -Operation {operation}",
                environmentVariables, 
                input);
            
            Console.WriteLine($"[TEST DEBUG] Exit code was {result.ExitCode}");
            Console.WriteLine($"[TEST DEBUG] Output was {result.Output}");
            Console.WriteLine($"[TEST DEBUG] Error was {result.Error}");
            
            result.ExitCode.Should().Be(0, "Bash script execution failed. Output: {result.Output}, Error: {result.Error}");
            return result;
        }

        ScriptExecutionResult ExecuteBashScript(string operation, string input = null)
        {
            Console.WriteLine($"[TEST DEBUG] Executing Bash script: {bashScript} with operation: {operation}");
            Console.WriteLine($"[TEST DEBUG] Calamari executable: {calamariExecutable}");
            Console.WriteLine($"[TEST DEBUG] Docker config path: {dockerConfigPath}");
            
            var environmentVariables = new Dictionary<string, string>
            {
                ["OCTOPUS_CALAMARI_EXECUTABLE"] = calamariExecutable,
                ["DOCKER_CONFIG"] = dockerConfigPath,
                ["OCTOPUS_CREDENTIAL_PASSWORD"] = TestEncryptionPassword
            };

            var result = ExecuteProcessWithInput("/bin/bash", 
                $"\"{bashScript}\" {operation}",
                environmentVariables, 
                input);

            Console.WriteLine($"[TEST DEBUG] Exit code was {result.ExitCode}");
            Console.WriteLine($"[TEST DEBUG] Output was {result.Output}");
            Console.WriteLine($"[TEST DEBUG] Error was {result.Error}");

            result.ExitCode.Should().Be(0, "Bash script execution failed. Output: {result.Output}, Error: {result.Error}");
            
            return result;
        }

        ScriptExecutionResult ExecuteProcessWithInput(string fileName, string arguments, Dictionary<string, string> environmentVariables, string input = null)
        {
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.WorkingDirectory = tempDirectory;

                // Set environment variables
                foreach (var env in environmentVariables)
                {
                    process.StartInfo.EnvironmentVariables[env.Key] = env.Value;
                }

                process.Start();

                if (input != null)
                {
                    process.StandardInput.WriteLine(input);
                }
                process.StandardInput.Close();

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new ScriptExecutionResult
                {
                    ExitCode = process.ExitCode,
                    Output = output,
                    Error = error
                };
            }
        }


        class ScriptExecutionResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
    }
}
