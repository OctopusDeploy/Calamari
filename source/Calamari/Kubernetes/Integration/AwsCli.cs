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

        public void Configure(string accessKey, string secretKey, string region, string sessionToken)
        {
            ExecuteAwsCommand("configure", "set", "aws_access_key_id", accessKey);
            ExecuteAwsCommand("configure", "set", "aws_secret_access_key", secretKey);
            ExecuteAwsCommand("configure", "set", "aws_default_region", region);
            ExecuteAwsCommand("configure", "set", "aws_session_token", sessionToken);
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

        public string GetEksClusterApiVersion(string clusterName, string region)
        {
            var result = ExecuteAwsCommand("eks", "get-token", $"--cluster-name={clusterName}", $"--region={region}");

            result.Result.VerifySuccess();

            var awsEksTokenCommand = string.Join("\n", result.Output.InfoLogs);

            try
            {
                return JObject.Parse(awsEksTokenCommand).SelectToken("apiVersion")?.ToString();
            }
            catch (Exception e)
            {
                throw new JsonReaderException($"Could not parse eks token: '{awsEksTokenCommand}'", e);
            }
        }

        CommandResultWithOutput ExecuteAwsCommand(params string[] arguments)
        {
            var args = arguments.Concat(new[] { "--profile octopus" }).ToArray();
            return ExecuteCommandAndReturnOutput(ExecutableLocation, args);
        }
    }
}