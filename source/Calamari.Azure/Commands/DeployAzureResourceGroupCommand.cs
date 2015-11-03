using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Microsoft.Azure;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.Azure.Commands
{
    [Command("deploy-azure-resource-group", Description = "Creates a new Azure Resource Group deployment")]
    public class DeployAzureResourceGroupCommand : Command
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string templateFile;
        private string templateParameterFile;

        public DeployAzureResourceGroupCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
            Options.Add("template=", "Path to the JSON template file.", v => templateFile = Path.GetFullPath(v));
            Options.Add("templateParameters=", "Path to the JSON template parameters file.", v => templateParameterFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            DeployResourceGroup(templateFile, templateParameterFile, variables);

            return 0;
        }

        private static void DeployResourceGroup(string templateFile, string templateParametersFile, CalamariVariableDictionary variables)
        {
            var fileSystem = new WindowsPhysicalFileSystem();

            var subscriptionId = variables[SpecialVariables.Action.Azure.SubscriptionId]; 
            var tenantId = variables[SpecialVariables.Action.Azure.TenantId]; 
            var clientId = variables[SpecialVariables.Action.Azure.ClientId];
            var password = variables[SpecialVariables.Action.Azure.Password];
            var resourceGroupName = variables[SpecialVariables.Action.Azure.ResourceGroupName];
            var deploymentName = !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName])
                ? variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName]
                : GenerateSlugFromStepName(variables[SpecialVariables.Action.Name]);
            var deploymentMode = (DeploymentMode)Enum.Parse(typeof (DeploymentMode),
                variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentMode]);

            var template = variables.Evaluate(fileSystem.ReadFile(templateFile));
            var parameters = variables.Evaluate(fileSystem.ReadFile(templateParametersFile));

            Log.Info($"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");

            using (var armClient = new ResourceManagementClient(
                new TokenCloudCredentials(subscriptionId, GetAuthorizationToken(tenantId, clientId, password))))
            {
                var result = armClient.Deployments.CreateOrUpdate(resourceGroupName, deploymentName,
                    new Microsoft.Azure.Management.Resources.Models.Deployment
                    {
                        Properties = new DeploymentProperties
                        {
                            Mode = deploymentMode,
                            Template = template, 
                            Parameters = parameters 
                        }
                    });

                Log.Info($"Deployment operation created: {result.Deployment.Id}");

                var previousPollWait = 0;
                var currentPollWait = 500;

                while (true)
                {
                   Thread.Sleep(currentPollWait);

                    Log.Verbose("Polling for status of deployment...");
                    var operation = armClient.DeploymentOperations.Get(resourceGroupName, deploymentName,
                        result.Deployment.Id);
                    Log.Verbose($"Provisioning state: {operation.Operation.Properties.ProvisioningState}");

                    switch (operation.Operation.Properties.ProvisioningState)
                    {
                        case "Succeeded":
                            Log.Info("Deployment complete.");
                            return;

                        case "Failed":
                            Log.Error($"Deployment failed. Status code: {operation.Operation.Properties.StatusCode}. Status message: {operation.Operation.Properties.StatusMessage}");
                            return;

                        default:
                            var temp = previousPollWait;
                            previousPollWait = currentPollWait;
                            currentPollWait = temp + currentPollWait;
                            break;
                    }


                }

            } 
        }

        static string GetAuthorizationToken(string tenantId, string clientId, string password)
        {
            var context = new AuthenticationContext($"https://login.windows.net/{tenantId}");
            var result = context.AcquireToken("https://management.core.windows.net/", new ClientCredential(clientId, password));
            return result.AccessToken;
        }

        static string GenerateSlugFromStepName(string stepName)
        {
            stepName = stepName ?? string.Empty;
            stepName = stepName.ToLower();
            stepName = Regex.Replace(stepName, "\\s", "-");
            stepName = new string(stepName.Select(x => (char.IsLetterOrDigit(x) || x == '-') ? x : '-').ToArray());
            stepName = Regex.Replace(stepName, "-+", "-");
            stepName = stepName.Trim('-', '/');
            return stepName;
        }
    }
}