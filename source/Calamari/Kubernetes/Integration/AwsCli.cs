using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes.Integration
{
    public class AwsCli : CommandLineTool
    {
        public AwsCli(
            ILog log,
            ICommandLineRunner commandLineRunner,
            string workingDirectory,
            Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
        }

        public bool TrySetAws()
        {
            log.Verbose("Attempting to authenticate with aws-cli");

            var result = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "aws.exe")
                : ExecuteCommandAndReturnOutput("which", "aws");

            var foundExecutable = result.Output.InfoLogs.FirstOrDefault();
            if (string.IsNullOrEmpty(foundExecutable))
            {
                log.Error("Could not find aws. Make sure aws is on the PATH.");
                return false;
            }

            ExecutableLocation = foundExecutable.Trim();

            return true;
        }

        public SemanticVersion GetAwsCliVersion()
        {
            var result = ExecuteAwsCommand("--version");
            result.Result.VerifySuccess();

            var awsCliVersion = result.Output.InfoLogs?.FirstOrDefault()
                                      ?.Split()
                                      .FirstOrDefault(versions => versions.StartsWith("aws-cli"))
                                      ?.Replace("aws-cli/", string.Empty);

            return new SemanticVersion(awsCliVersion);
        }

        public string GetEksClusterToken(string clusterName, string region)
        {
            var result = ExecuteAwsCommand("eks", "get-token", $"--cluster-name={clusterName}", $"--region={region}");

            result.Result.VerifySuccess();

            var jsonString = string.Join("\n", result.Output.InfoLogs);

            try
            {
                return JObject.Parse(jsonString).SelectToken("status")?.SelectToken("token")?.ToString();
            }
            catch (Exception e)
            {
                throw new JsonReaderException($"Could not parse eks token: '{jsonString}'", e);
            }
        }

        public string GetEksClusterApiVersion(string clusterName, string region)
        {
            var result = ExecuteAwsCommand("eks",
                                           "get-token",
                                           $"--cluster-name={clusterName}",
                                           $"--region={region}");

            result.Result.VerifySuccess();

            var jsonString = string.Join("\n", result.Output.InfoLogs);

            return JObject.Parse(jsonString).SelectToken("apiVersion")?.ToString();
        }

        CommandResultWithOutput ExecuteAwsCommand(params string[] arguments)
            => ExecuteCommandAndReturnOutput(ExecutableLocation, arguments);
    }
}