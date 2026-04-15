using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Calamari.CloudAccounts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes.Authentication
{
    public class AwsCliAuth
    {
        readonly AwsCli awsCli;
        readonly IKubectl kubectl;
        readonly IVariables deploymentVariables;
        readonly Dictionary<string, string> environmentVars;
        readonly ILog log;

        public AwsCliAuth(
            AwsCli awsCli,
            IKubectl kubectl,
            IVariables deploymentVariables,
            Dictionary<string, string> environmentVars,
            ILog log)
        {
            this.awsCli = awsCli;
            this.kubectl = kubectl;
            this.deploymentVariables = deploymentVariables;
            this.environmentVars = environmentVars;
            this.log = log;
        }

        public void Configure(string @namespace, string clusterUrl, string user)
        {
            var clusterName = deploymentVariables.Get(SpecialVariables.EksClusterName);
            var region = GetEksClusterRegion(clusterUrl);

            log.Info(
                $"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using EKS cluster name {clusterName}");

            if (TrySetKubeConfigAuthenticationToAwsCli(clusterName, region, user))
                return;

            SetKubeConfigAuthenticationToAwsIAm(user, clusterName, region);
        }

        bool TrySetKubeConfigAuthenticationToAwsCli(string clusterName, string region, string user)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                log.Verbose("Could not determine AWS region from the EKS cluster URL. The AWS CLI requires a region to authenticate. Falling back to aws-iam-authenticator.");
                return false;
            }

            if (!awsCli.TrySetAws())
            {
                log.Verbose("Could not find the aws cli, falling back to the aws-iam-authenticator.");
                return false;
            }

            ConfigureAwsCli();

            try
            {
                var awsCliVersion = awsCli.GetAwsCliVersion();
                var minimumAwsCliVersionForAuth = new SemanticVersion("1.16.156");
                if (awsCliVersion.CompareTo(minimumAwsCliVersionForAuth) > 0)
                {
                    //Certain customers have had issues with the AWS CLI token only being a 15min fixed expiry
                    //This feature toggle changes to use the kubectl config set-credentials using exec, which handles the expired token
                    if (FeatureToggle.KubernetesAuthAwsCliWithExecFeatureToggle.IsEnabled(deploymentVariables))
                    {
                        SetKubeConfigAuthenticationToAwsCliUsingExec(user, clusterName, region);
                    }
                    else
                    {
                        SetKubeConfigAuthenticationToAwsCliUsingToken(user, clusterName, region);
                    }

                    return true;
                }

                log.Verbose(
                    $"aws cli version: {awsCliVersion} does not support the \"aws eks get-token\" command. Please update to a version later than 1.16.156");
            }
            catch (Exception e)
            {
                log.Verbose(
                    $"Unable to authenticate using the aws cli. Failed with error message: {e.Message}");
            }

            return false;
        }

        void SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName, string region)
        {
            log.Verbose("Attempting to authenticate with aws-iam-authenticator");

            var apiVersion = GetKubeCtlAuthApiVersion();

            var arguments = new List<string>
            {
                "config",
                "set-credentials",
                user,
                "--exec-command=aws-iam-authenticator",
                $"--exec-api-version={apiVersion}",
                "--exec-arg=token",
                "--exec-arg=-i",
                $"--exec-arg={clusterName}"
            };

            // Pass the region to aws-iam-authenticator if available, avoiding an unnecessary IMDS lookup and ensuring the correct regional STS endpoint is used.
            // When region is not available (e.g. CNAME cluster URL), aws-iam-authenticator resolves the region itself via IMDS or falls back to us-east-1.
            if (!string.IsNullOrWhiteSpace(region))
            {
                arguments.AddRange(["--exec-arg=--region", $"--exec-arg={region}"]);
            }

            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }

        string GetKubeCtlAuthApiVersion()
        {
            var kubectlVersion = kubectl.GetVersion();

            //v1alpha1 was deprecated in 1.24 of K8s
            return kubectlVersion.Some() && kubectlVersion.Value > new SemanticVersion("1.23.6")
                ? "client.authentication.k8s.io/v1beta1"
                : "client.authentication.k8s.io/v1alpha1";
        }

        void ConfigureAwsCli()
        {
            if (!environmentVars.ContainsKey("AWS_ACCESS_KEY_ID"))
            {
                var awsEnvironmentGeneration = AwsEnvironmentGeneration.Create(log, deploymentVariables).GetAwaiter().GetResult();
                environmentVars.AddRange(awsEnvironmentGeneration.EnvironmentVars);
            }
        }

        static string GetEksClusterRegion(string clusterUrl)
        {
            var match = Regex.Match(clusterUrl, @"^https:\/\/[^.]+(?:\.[^.]+)?\.([a-z0-9-]+)\.eks\.amazonaws\.com$");
            return match.Success ? match.Groups[1].Value : null;
        }

        void SetKubeConfigAuthenticationToAwsCliUsingToken(string user, string clusterName, string region)
        {
            var token = awsCli.GetEksClusterToken(clusterName, region);

            var arguments = new List<string> { "config", "set-credentials", user, $"--token={token}" };

            log.AddValueToRedact(token, "<token>");
            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }

        void SetKubeConfigAuthenticationToAwsCliUsingExec(string user, string clusterName, string region)
        {
            var apiVersion = GetKubeCtlAuthApiVersion();

            var arguments = new List<string>
            {
                "config",
                "set-credentials",
                user,
                "--exec-command=aws",
                "--exec-arg=eks",
                "--exec-arg=get-token",
                $"--exec-arg=--cluster-name={clusterName}",
                $"--exec-arg=--region={region}",
                $"--exec-api-version={apiVersion}"
            };

            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }
    }
}
