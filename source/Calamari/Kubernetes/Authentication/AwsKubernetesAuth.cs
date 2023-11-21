using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json.Linq;
using Octopus.Versioning.Semver;
using Octopus.CoreUtilities;

namespace Calamari.Kubernetes.Authentication
{
    public class AwsKubernetesAuth
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IKubectl kubectl;
        readonly Dictionary<string, string> environmentVars;
        readonly string workingDirectory;
        string aws;

        public AwsKubernetesAuth(
            IVariables variables,
            ILog log,
            ICommandLineRunner commandLineRunner,
            IKubectl kubectl,
            Dictionary<string, string> environmentVars,
            string workingDirectory)
        {
            this.variables = variables;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.environmentVars = environmentVars;
            this.workingDirectory = workingDirectory;
        }

        public void SetupContextForAmazonServiceAccount(string @namespace, string clusterUrl, string user)
        {
            var clusterName = variables.Get(SpecialVariables.EksClusterName);
            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using EKS cluster name {clusterName}");

            if (TrySetKubeConfigAuthenticationToAwsCli(clusterName, clusterUrl, user))
            {
                return;
            }

            log.Verbose("Attempting to authenticate with aws-iam-authenticator");
            SetKubeConfigAuthenticationToAwsIAm(user, clusterName);
        }

        bool TrySetKubeConfigAuthenticationToAwsCli(string clusterName, string clusterUrl, string user)
        {
            log.Verbose("Attempting to authenticate with aws-cli");
            if (!TrySetAws())
            {
                log.Verbose("Could not find the aws cli, falling back to the aws-iam-authenticator.");
                return false;
            }

            try {
                var awsCliVersion = GetAwsCliVersion();
                var minimumAwsCliVersionForAuth = new SemanticVersion("1.16.156");
                if (awsCliVersion.CompareTo(minimumAwsCliVersionForAuth) > 0)
                {
                    var region = GetEksClusterRegion(clusterUrl);
                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        var apiVersion = GetEksClusterApiVersion(clusterName, region);
                        SetKubeConfigAuthenticationToAwsCli(user, clusterName, region, apiVersion);
                        return true;
                    }

                    log.Verbose("The EKS cluster Url specified should contain a valid aws region name");
                }

                log.Verbose($"aws cli version: {awsCliVersion} does not support the \"aws eks get-token\" command. Please update to a version later than 1.16.156");
            }
            catch (Exception e)
            {
                log.Verbose($"Unable to authenticate to {clusterUrl} using the aws cli. Failed with error message: {e.Message}");
            }

            return false;
        }

        string GetEksClusterRegion(string clusterUrl) => clusterUrl.Replace(".eks.amazonaws.com", "").Split('.').Last();

        SemanticVersion GetAwsCliVersion()
        {
            var awsCliCommandRes = ExecuteCommandAndReturnOutput(aws, "--version").FirstOrDefault();
            var awsCliVersionString = awsCliCommandRes.Split()
                                                      .FirstOrDefault(versions => versions.StartsWith("aws-cli"))
                                                      .Replace("aws-cli/", string.Empty);
            return new SemanticVersion(awsCliVersionString);
        }

        string GetEksClusterApiVersion(string clusterName, string region)
        {
            var logLines = ExecuteCommandAndReturnOutput(aws,
                "eks",
                "get-token",
                $"--cluster-name={clusterName}",
                $"--region={region}");
            var awsEksTokenCommand = string.Join("\n", logLines);
            return JObject.Parse(awsEksTokenCommand).SelectToken("apiVersion").ToString();
        }

        void SetKubeConfigAuthenticationToAwsCli(string user, string clusterName, string region, string apiVersion)
        {
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, "--exec-command=aws", "--exec-arg=eks", "--exec-arg=get-token", $"--exec-arg=--cluster-name={clusterName}", $"--exec-arg=--region={region}", $"--exec-api-version={apiVersion}");
        }

        void SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName)
        {
            var kubectlVersion = kubectl.GetVersion();
            var apiVersion = kubectlVersion.Some() && kubectlVersion.Value > new SemanticVersion("1.23.6")
                ? "client.authentication.k8s.io/v1beta1"
                : "client.authentication.k8s.io/v1alpha1";

            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, "--exec-command=aws-iam-authenticator", $"--exec-api-version={apiVersion}", "--exec-arg=token", "--exec-arg=-i", $"--exec-arg={clusterName}");
        }

        bool TrySetAws()
        {
            aws = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "aws.exe").FirstOrDefault()
                : ExecuteCommandAndReturnOutput("which", "aws").FirstOrDefault();

            if (string.IsNullOrEmpty(aws))
            {
                return false;
            }

            aws = aws.Trim();

            return true;
        }

        IEnumerable<string> ExecuteCommandAndReturnOutput(string exe, params string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                EnvironmentVars = environmentVars,
                WorkingDirectory = workingDirectory,
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput
            };

            var result = commandLineRunner.Execute(invocation);

            return result.ExitCode == 0
                ? captureCommandOutput.InfoLogs.ToArray()
                : Enumerable.Empty<string>();
        }
    }
}