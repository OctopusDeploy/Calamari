using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    public class ScriptFixture : CalamariFixture
    {
        const string PathEnvironmentVariableName = "PATH";
        const string PythonPathEnvironmentVariableName = "PYTHONPATH";

        [OneTimeSetUp]
        public void InstallTools()
        {
            void AddAwsCliToPath(string destination, string installPath)
            {
                var path = Environment.GetEnvironmentVariable(PathEnvironmentVariableName) ?? string.Empty; 
                if (!path.Contains(installPath))
                {
                    var pathWithAwsCli =
                        $"{installPath}{Path.PathSeparator}{path}";
                    Environment.SetEnvironmentVariable(PathEnvironmentVariableName, pathWithAwsCli);
                }
                
                var pythonPathWithAwsCli =
                    Environment.GetEnvironmentVariable(PythonPathEnvironmentVariableName) is object
                        ? $"{destination}{Path.PathSeparator}{Environment.GetEnvironmentVariable(PythonPathEnvironmentVariableName)}"
                        : destination;
                Environment.SetEnvironmentVariable(PythonPathEnvironmentVariableName, pythonPathWithAwsCli);
            }

            void InstallAwsCli(string destination, string installPath)
            {
                Directory.CreateDirectory(installPath);
                var installAwsCliScriptPath = Path.Combine(destination, GetScriptFileName("install-awscli"));
                var scriptBody = $"pip install awscli --target {destination} --upgrade --no-cache-dir --force-reinstall --disable-pip-version-check --no-warn-script-location";
                using (new TemporaryFile(installAwsCliScriptPath))
                {
                    File.WriteAllText(installAwsCliScriptPath, scriptBody);
                    var (output, _) = RunScript(installAwsCliScriptPath);
                    output.AssertSuccess();
                }

                AddAwsCliToPath(destination, installPath);
            }

            var destinationDirectoryName = TestEnvironment.GetTestPath("AWSCLIPath");
            var binPath = Path.Combine(destinationDirectoryName, "bin");
            if (Directory.Exists(destinationDirectoryName) && Directory.GetFiles(binPath).Any())
            {
                AddAwsCliToPath(destinationDirectoryName, binPath);
                return;
            }

            InstallAwsCli(destinationDirectoryName, binPath);
        }

        [OneTimeTearDown]
        public void RemoveTools()
        {
            void RemoveAwsCliFromPath(string destination, string installPath)
            {
                var path = Environment.GetEnvironmentVariable(PathEnvironmentVariableName) ?? string.Empty;
                if (path.Contains(installPath))
                {
                    var pathWithOutAwsCli = path
                        .Replace(installPath, "")
                        .Trim(Path.PathSeparator);
                    Environment.SetEnvironmentVariable(PathEnvironmentVariableName, pathWithOutAwsCli);
                }

                var pythonPath = Environment.GetEnvironmentVariable(PythonPathEnvironmentVariableName) ?? string.Empty; 
                if (pythonPath.Contains(destination))
                {
                    var pythonPathWithoutAwsCli = pythonPath
                        .Replace(destination, "")
                        .Trim(Path.PathSeparator);
                    Environment.SetEnvironmentVariable(PythonPathEnvironmentVariableName,
                        pythonPathWithoutAwsCli.Length > 0 ? pythonPathWithoutAwsCli : null);
                }
            }
            
            void UninstallAwsCli(string destination, string installPath)
            {
                RemoveAwsCliFromPath(destination, installPath);
                Directory.Delete(destination, true);
            }
            
            var destinationDirectoryName = TestEnvironment.GetTestPath("AWSCLIPath");
            var binPath = Path.Combine(destinationDirectoryName, "bin");
            if (!Directory.Exists(destinationDirectoryName) || !Directory.Exists(binPath))
            {
                RemoveAwsCliFromPath(destinationDirectoryName, binPath);
                return;
            }

            UninstallAwsCli(destinationDirectoryName, binPath);
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