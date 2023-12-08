using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.CloudAccounts;
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

        public bool TryConfigure(string @namespace, string clusterUrl, string user)
        {
            var clusterName = deploymentVariables.Get(SpecialVariables.EksClusterName);
            log.Info(
                $"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using EKS cluster name {clusterName}");

            if (TrySetKubeConfigAuthenticationToAwsCli(clusterName, clusterUrl, user))
                return true;

            log.Verbose("Attempting to authenticate with aws-iam-authenticator");
            return SetKubeConfigAuthenticationToAwsIAm(user, clusterName);
        }

        bool TrySetKubeConfigAuthenticationToAwsCli(string clusterName, string clusterUrl, string user)
        {
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
                    var region = GetEksClusterRegion(clusterUrl);
                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        var apiVersion = awsCli.GetEksClusterApiVersion(clusterName, region);
                        SetKubeConfigAuthenticationToAwsCli(user, clusterName, region, apiVersion);
                        return true;
                    }

                    log.Verbose("The EKS cluster Url specified should contain a valid aws region name");
                }

                log.Verbose(
                    $"aws cli version: {awsCliVersion} does not support the \"aws eks get-token\" command. Please update to a version later than 1.16.156");
            }
            catch (Exception e)
            {
                log.Verbose(
                    $"Unable to authenticate to {clusterUrl} using the aws cli. Failed with error message: {e.Message}");
            }

            return false;
        }

        bool SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName)
        {
            var kubectlVersion = kubectl.GetVersion();
            var apiVersion = kubectlVersion.Some() && kubectlVersion.Value > new SemanticVersion("1.23.6")
                ? "client.authentication.k8s.io/v1beta1"
                : "client.authentication.k8s.io/v1alpha1";

            kubectl.ExecuteCommandAndAssertSuccess(
                "config",
                "set-credentials",
                user,
                "--exec-command=aws-iam-authenticator",
                $"--exec-api-version={apiVersion}",
                "--exec-arg=token",
                "--exec-arg=-i",
                $"--exec-arg={clusterName}");

            return true;
        }

        void ConfigureAwsCli()
        {
            if (!environmentVars.ContainsKey("AWS_ACCESS_KEY_ID"))
            {
                var awsEnvironmentGeneration =
                    AwsEnvironmentGeneration.Create(log, deploymentVariables).GetAwaiter().GetResult();
                environmentVars.AddRange(awsEnvironmentGeneration.EnvironmentVars);
            }

            awsCli.Configure(
                GetEnvironmentVarOrDefault("AWS_ACCESS_KEY_ID"),
                GetEnvironmentVarOrDefault("AWS_SECRET_ACCESS_KEY"),
                GetEnvironmentVarOrDefault("AWS_REGION"),
                GetEnvironmentVarOrDefault("AWS_SESSION_TOKEN")
            );
        }

        string GetEnvironmentVarOrDefault(string key) => environmentVars.TryGetValue(key, out var value) ? value : null;

        string GetEksClusterRegion(string clusterUrl) => clusterUrl.Replace(".eks.amazonaws.com", "").Split('.').Last();

        void SetKubeConfigAuthenticationToAwsCli(string user, string clusterName, string region, string apiVersion)
        {
            var oidcJwt = deploymentVariables.Get(AccountVariables.Jwt);
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

            if (!oidcJwt.IsNullOrEmpty())
            {
                arguments.Add($"--token={oidcJwt}");
            }

            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }
    }
}