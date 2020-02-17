using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Retry;
using Calamari.Integration.Scripting;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    [RequiresNonFreeBSDPlatform]
    [Category(TestCategory.CompatibleOS.OnlyNix)]
    public class ScriptFixture : CalamariFixture
    {
        const string PathEnvironmentVariableName = "PATH";

        [OneTimeSetUp]
        public async Task InstallTools()
        {
            void AddAwsCliToPath(string binPath)
            {
                var path = Environment.GetEnvironmentVariable(PathEnvironmentVariableName) ?? string.Empty;
                if (path.Contains(binPath)) return;
                
                var pathWithAwsCli =
                    $"{binPath}{Path.PathSeparator}{path}";
                Environment.SetEnvironmentVariable(PathEnvironmentVariableName, pathWithAwsCli);
            }

            async Task InstallAwsCli(string destination, string binPath)
            {
                Console.WriteLine("Downloading aws cli...");
                var retry = new RetryTracker(3, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(1000, 30000, 2));
                while (retry.Try())
                {
                    try
                    {
                        ServicePointManager.SecurityProtocol =
                            SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                        using (var client = new HttpClient())
                        {
                            var zipPath = Path.Combine(Path.GetTempPath(), "awscliv2.zip");
                            using (new TemporaryFile(zipPath))
                            {
                                using (var fileStream =
                                    new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                using (var stream = await client.GetStreamAsync($"https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip"))
                                {
                                    await stream.CopyToAsync(fileStream);
                                }

                                ZipFile.ExtractToDirectory(zipPath, destination);
                            }
                        }
                        
                        break;
                    }
                    catch
                    {
                        if (!retry.CanRetry())
                            throw;

                        await Task.Delay(retry.Sleep());
                    }
                }
                
                AddAwsCliToPath(binPath);
            }

            var destinationDirectoryName = TestEnvironment.GetTestPath("AWSCLIPath");
            var awsCliPath = GetAwsCliPath(destinationDirectoryName);
            if (AwsCliInstalled(destinationDirectoryName, awsCliPath))
            {
                AddAwsCliToPath(awsCliPath);
                return;
            }

            await InstallAwsCli(destinationDirectoryName, awsCliPath);
        }

        [OneTimeTearDown]
        public void RemoveTools()
        {
            void RemoveAwsCliFromPath(string destination)
            {
                var path = Environment.GetEnvironmentVariable(PathEnvironmentVariableName) ?? string.Empty;
                if (!path.Contains(destination)) return;
                
                var pathWithOutAwsCli = path
                    .Replace(destination, "")
                    .Trim(Path.PathSeparator);
                Environment.SetEnvironmentVariable(PathEnvironmentVariableName, pathWithOutAwsCli);
            }
            
            void UninstallAwsCli(string destination, string binPath)
            {
                RemoveAwsCliFromPath(binPath);
                Directory.Delete(destination, true);
            }
            
            var destinationDirectoryName = TestEnvironment.GetTestPath("AWSCLIPath");
            var awsCliPath = GetAwsCliPath(destinationDirectoryName);
            if (!AwsCliInstalled(destinationDirectoryName, awsCliPath))
            {
                RemoveAwsCliFromPath(destinationDirectoryName);
                return;
            }

            UninstallAwsCli(destinationDirectoryName, awsCliPath);
        }

        static bool AwsCliInstalled(string destination, string binPath)
        {
            if (!Directory.Exists(destination)) return false;
            
            var path = Directory.EnumerateFiles(binPath).FirstOrDefault();
            return path is object;
        }
        
        static string GetAwsCliPath(string destination)
        {
            return Path.Combine(destination, "aws", "dist");
        }

        [Test]
        public void RunScript()
        {
            var (output, _) = RunScript(
                GetScriptFileName("awsscript"),
                GetAdditionalVariables(),
                new Dictionary<string, string> {{"extensions", "Calamari.Aws"}}
            );

            output.AssertSuccess();
            output.AssertOutput("user/OctopusAPITester");
        }

        static Dictionary<string, string> GetAdditionalVariables()
        {
            return new Dictionary<string, string>
            {
                {"Octopus.Action.AwsAccount.Variable", "AwsAccount"},
                {"Octopus.Action.Aws.Region", "us-east-1"},
                {"AwsAccount.AccessKey", ExternalVariables.Get(ExternalVariable.AwsAcessKey)},
                {"AwsAccount.SecretKey", ExternalVariables.Get(ExternalVariable.AwsSecretKey)},
                {"Octopus.Action.Aws.AssumeRole", "False"},
                {"Octopus.Action.Aws.AssumedRoleArn", ""},
                {"Octopus.Action.Aws.AssumedRoleSession", ""},
            };
        }

        static string GetScriptFileName(string fileName) =>
            $"{fileName}.{(CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell : ScriptSyntax.Bash).FileExtension()}";
    }
}