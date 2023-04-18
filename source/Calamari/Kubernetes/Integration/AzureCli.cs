using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    public class AzureCli : CommandLineTool
    {
        public AzureCli(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
        }

        public bool TrySetAz()
        {
            var result = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "az.cmd")
                : ExecuteCommandAndReturnOutput("which", "az");

            var foundExecutable = result.Output.Messages.FirstOrDefault()?.Text;
            if (string.IsNullOrEmpty(foundExecutable))
            {
                log.Error("Could not find az. Make sure az is on the PATH.");
                return false;
            }

            ExecutableLocation = foundExecutable.Trim();
            return true;
        }

        public void ConfigureAzAccount(string subscriptionId,
                                       string tenantId,
                                       string clientId,
                                       string password,
                                       string azEnvironment)
        {
            environmentVars.Add("AZURE_CONFIG_DIR", Path.Combine(workingDirectory, "azure-cli"));

            TryExecuteCommandAndLogOutput(ExecutableLocation,
                                          "cloud",
                                          "set",
                                          "--name",
                                          azEnvironment);

            log.Verbose("Azure CLI: Authenticating with Service Principal");

            // Use the full argument with an '=' because of https://github.com/Azure/azure-cli/issues/12105
            ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation,
                                                                 "login",
                                                                 "--service-principal",
                                                                 $"--username=\"{clientId}\"",
                                                                 $"--password=\"{password}\"",
                                                                 $"--tenant=\"{tenantId}\""));

            log.Verbose($"Azure CLI: Setting active subscription to {subscriptionId}");
            ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation,
                                                                 "account",
                                                                 "set",
                                                                 "--subscription",
                                                                 subscriptionId));

            log.Info("Successfully authenticated with the Azure CLI");
        }

        public void ConfigureAksKubeCtlAuthentication(Kubectl kubectlCli, string clusterResourceGroup, string clusterName, string clusterNamespace, string kubeConfigPath, bool adminLogin)
        {
            log.Info($"Creating kubectl context to AKS Cluster in resource group {clusterResourceGroup} called {clusterName} (namespace {clusterNamespace}) using a AzureServicePrincipal");

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
    }
}
