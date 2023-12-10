﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    public class AzureCli : CommandLineTool
    {
        public AzureCli(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
        }

        public void SetAz()
        {
            var result = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "az.cmd")
                : ExecuteCommandAndReturnOutput("which", "az");

            var foundExecutable = result.Output.InfoLogs.FirstOrDefault();
            if (string.IsNullOrEmpty(foundExecutable))
            {
                throw new KubectlException("Could not find az. Make sure az is on the PATH.");
            }

            ExecutableLocation = foundExecutable.Trim();
        }

        public void ConfigureAzAccount(string subscriptionId,
                                       string tenantId,
                                       string clientId,
                                       string credential,
                                       string azEnvironment,
                                       bool isOidc)
        {
            SetConfigDirectoryEnvironmentVariable(environmentVars, workingDirectory);

            TryExecuteCommandAndLogOutput(ExecutableLocation,
                                          "cloud",
                                          "set",
                                          "--name",
                                          azEnvironment);

            if (isOidc)
            {
                log.Verbose("Azure CLI: Authenticating with OpenID Connect Access Token");
                ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation,
                                                                     "login",
                                                                     "--service-principal",
                                                                     "--federated-token",
                                                                     $"\"{credential}\"",
                                                                     $"--username=\"{clientId}\"",
                                                                     $"--tenant=\"{tenantId}\""));
            }
            else
            {
                // Use the full argument with an '=' because of https://github.com/Azure/azure-cli/issues/12105
                log.Verbose("Azure CLI: Authenticating with Service Principal");
                ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation,
                                                                     "login",
                                                                     "--service-principal",
                                                                     $"--username=\"{clientId}\"",
                                                                     $"--password=\"{credential}\"",
                                                                     $"--tenant=\"{tenantId}\""));
            }

            log.Verbose($"Azure CLI: Setting active subscription to {subscriptionId}");
            ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation,
                                                                 "account",
                                                                 "set",
                                                                 "--subscription",
                                                                 subscriptionId));

            log.Info("Successfully authenticated with the Azure CLI");
        }

        public void ConfigureAksKubeCtlAuthentication(IKubectl kubectlCli, string clusterResourceGroup, string clusterName, string clusterNamespace, string kubeConfigPath, bool adminLogin)
        {
            log.Info($"Creating kubectl context to AKS Cluster in resource group {clusterResourceGroup} called {clusterName} (namespace {clusterNamespace})");

            var arguments = new List<string>(new[]
            {
                "aks",
                "get-credentials",
                "--resource-group",
                clusterResourceGroup,
                "--name",
                clusterName,
                "--file",
                $"\"{kubeConfigPath}\"",
                "--overwrite-existing"
            });
            if (adminLogin)
            {
                arguments.Add("--admin");
                clusterName += "-admin";
            }

            var result = ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, arguments.ToArray()));
            result.VerifySuccess();

            kubectlCli.ExecuteCommandAndAssertSuccess("config", "set-context", clusterName, $"--namespace={@clusterNamespace}");
        }

        public static void SetConfigDirectoryEnvironmentVariable(IDictionary<string, string> environmentVars, string workingDirectory)
        {
            environmentVars.Add("AZURE_CONFIG_DIR", Path.Combine(workingDirectory, "azure-cli"));
        }
    }
}