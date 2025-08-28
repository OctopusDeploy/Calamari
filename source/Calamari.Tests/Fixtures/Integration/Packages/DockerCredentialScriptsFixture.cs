using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Integration.Packages.Download;
using Calamari.Testing.Requirements;
using FluentAssertions;
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
            File.WriteAllText(bashScript, bashContent);
            
            // Make bash script executable on Unix systems
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                SilentProcessRunner.ExecuteCommand("chmod", $"+x {bashScript}", ".", new Dictionary<string, string>(), _ => { }, _ => { });
            }
        }

        void SetupCalamariExecutable()
        {
            // Use the real Calamari executable for true integration testing
            var testAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var testBinDirectory = Path.GetDirectoryName(testAssemblyLocation);
            
            // Try to find Calamari executable in the same directory as the test assembly
            // On Unix systems it's "Calamari", on Windows it's "Calamari.exe"
            var executableNames = Environment.OSVersion.Platform == PlatformID.Win32NT 
                ? new[] { "Calamari.exe", "Calamari" }
                : new[] { "Calamari", "Calamari.exe" };
            
            foreach (var executableName in executableNames)
            {
                calamariExecutable = Path.Combine(testBinDirectory, executableName);
                if (File.Exists(calamariExecutable))
                {
                    return;
                }
            }
            
            throw new InvalidOperationException($"Could not find Calamari executable in {testBinDirectory}. Tried: {string.Join(", ", executableNames)}. Make sure the project is built.");
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
            result.ExitCode.Should().Be(0);
            
            // Verify credentials were actually stored by the real command
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.Exists(credentialsDir).Should().BeTrue();
            Directory.GetFiles(credentialsDir, "*.cred").Should().HaveCount(1);
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
            result.ExitCode.Should().Be(0);
            
            // Parse and verify the returned JSON credentials
            // Extract the JSON line from Calamari output (skip verbose logging)
            var outputLines = result.Output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var jsonLine = outputLines.FirstOrDefault(line => line.Trim().StartsWith("{"));
            jsonLine.Should().NotBeNull("Expected JSON response from credential get operation");
            
            var responseJson = JsonConvert.DeserializeObject<dynamic>(jsonLine.Trim());
            ((string)responseJson.Username).Should().Be(TestUsername);
            ((string)responseJson.Secret).Should().Be(TestPassword);
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
            ExecutePowerShellScript("store", credentialJson);
            
            // Verify credential exists
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.GetFiles(credentialsDir, "*.cred").Should().HaveCount(1);

            // Act
            var result = ExecutePowerShellScript("erase", TestServerUrl);

            // Assert
            result.ExitCode.Should().Be(0);
            
            // Verify credential was actually erased
            Directory.GetFiles(credentialsDir, "*.cred").Should().BeEmpty();
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
            result.ExitCode.Should().Be(0);
            
            // Verify credentials were actually stored by the real command
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.Exists(credentialsDir).Should().BeTrue();
            Directory.GetFiles(credentialsDir, "*.cred").Should().HaveCount(1);
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
            result.ExitCode.Should().Be(0);
            
            // Parse and verify the returned JSON credentials
            // Extract the JSON line from Calamari output (skip verbose logging)
            var outputLines = result.Output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var jsonLine = outputLines.FirstOrDefault(line => line.Trim().StartsWith("{"));
            jsonLine.Should().NotBeNull("Expected JSON response from credential get operation");
            
            var responseJson = JsonConvert.DeserializeObject<dynamic>(jsonLine.Trim());
            ((string)responseJson.Username).Should().Be(TestUsername);
            ((string)responseJson.Secret).Should().Be(TestPassword);
        }

        [Test]
        [NonWindowsTest]
        public void BashScript_WithEraseOperation_CallsCalamariCorrectly()
        {
            // Arrange - First store credentials
            var credentialJson = JsonConvert.SerializeObject(new
            {
                ServerURL = TestServerUrl,
                Username = TestUsername,
                Secret = TestPassword
            });
            ExecuteBashScript("store", credentialJson);
            
            // Verify credential exists
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.GetFiles(credentialsDir, "*.cred").Should().HaveCount(1);

            // Act
            var result = ExecuteBashScript("erase", TestServerUrl);

            // Assert
            result.ExitCode.Should().Be(0);
            
            // Verify credential was actually erased
            Directory.GetFiles(credentialsDir, "*.cred").Should().BeEmpty();
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
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{powershellScript}\" {operation}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = tempDirectory
            };
            
            // Set the environment variable for the real Calamari executable
            psi.EnvironmentVariables["OCTOPUS_CALAMARI_EXECUTABLE"] = calamariExecutable;
            
            // Set up environment for real docker-credential command
            psi.EnvironmentVariables["DOCKER_CONFIG"] = dockerConfigPath;
            psi.EnvironmentVariables["OCTOPUS_CREDENTIAL_PASSWORD"] = TestEncryptionPassword;

            using (var process = new System.Diagnostics.Process { StartInfo = psi })
            {
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

        ScriptExecutionResult ExecuteBashScript(string operation, string input = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{bashScript}\" {operation}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = tempDirectory
            };
            
            // Set the environment variable for the real Calamari executable
            psi.EnvironmentVariables["OCTOPUS_CALAMARI_EXECUTABLE"] = calamariExecutable;
            
            // Set up environment for real docker-credential command
            psi.EnvironmentVariables["DOCKER_CONFIG"] = dockerConfigPath;
            psi.EnvironmentVariables["OCTOPUS_CREDENTIAL_PASSWORD"] = TestEncryptionPassword;

            using (var process = new System.Diagnostics.Process { StartInfo = psi })
            {
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
