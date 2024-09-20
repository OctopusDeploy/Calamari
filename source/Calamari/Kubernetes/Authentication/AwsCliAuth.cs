#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.CloudAccounts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning.Semver;
using InvalidOperationException = Amazon.CloudFormation.Model.InvalidOperationException;

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
            log.Info(
                $"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using EKS cluster name {clusterName}");

            if (TrySetKubeConfigAuthenticationToAwsCli(clusterName, clusterUrl, user))
                return;

            log.Verbose("Attempting to authenticate with aws-iam-authenticator");
            SetKubeConfigAuthenticationToAwsIAm(user, clusterName);
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
                        //Certain customers have had issues with the AWS Cli token only being a 15min fixed expiry
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

        void SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName)
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
        }

        void ConfigureAwsCli()
        {
            if (!environmentVars.ContainsKey("AWS_ACCESS_KEY_ID"))
            {
                var awsEnvironmentGeneration = AwsEnvironmentGeneration.Create(log, deploymentVariables).GetAwaiter().GetResult();
                ListExtensions.AddRange(environmentVars, awsEnvironmentGeneration.EnvironmentVars);
            }
        }

        static string GetEksClusterRegion(string clusterUrl) => clusterUrl.Replace(".eks.amazonaws.com", "").Split('.').Last();

        void SetKubeConfigAuthenticationToAwsCliUsingToken(string user, string clusterName, string region)
        {
            var token = deploymentVariables.Get(AccountVariables.Jwt);

            if (string.IsNullOrEmpty(token))
            {
                token = awsCli.GetEksClusterToken(clusterName, region);
            }

            var arguments = new List<string> { "config", "set-credentials", user, $"--token={token}" };

            log.AddValueToRedact(token, "<token>");
            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }
        
        void SetKubeConfigAuthenticationToAwsCliUsingExec(string user, string clusterName, string region)
        {
            var oidcJwt = deploymentVariables.Get(AccountVariables.Jwt);

            var apiVersion = awsCli.GetEksClusterApiVersion(clusterName, region);

            if (apiVersion == null)
            {
                throw new InvalidOperationException($"Unable to determine API version for cluster {clusterName} in region {region}.");
            }
            
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
                log.AddValueToRedact(oidcJwt, "<token>");
            }

            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }
    }
}
#endif