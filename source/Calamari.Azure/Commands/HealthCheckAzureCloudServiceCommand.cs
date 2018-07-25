using System;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Azure.Accounts;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.Azure.Commands
{
    [Command("hc-azure-cs", Description = "Run a health check on an Azure Cloud Service")]
    public class HealthCheckAzureCloudServiceCommand : Command
    {
        private readonly ILog log;
        private readonly ICertificateStore certificateStore;
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public HealthCheckAzureCloudServiceCommand(ILog log, ICertificateStore certificateStore)
        {
            this.log = log;
            this.certificateStore = certificateStore;
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var account = AccountFactory.Create(variables);

            var cloudServiceName = variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            
            if (account is AzureAccount azureAccount)
            {
                using (var azureClient = azureAccount.CreateComputeManagementClient(certificateStore))
                {
                    var azureResponse = azureClient.HostedServices.List();
                    if (azureResponse.StatusCode != HttpStatusCode.OK)
                        throw new Exception("Azure returned HTTP status-code " + azureResponse.StatusCode);

                    var hostedService = azureResponse.HostedServices.FirstOrDefault(hs => hs.ServiceName == cloudServiceName);
                    if (hostedService == null)
                        throw new Exception($"Hosted service with name {cloudServiceName} was not found.");
                }
            }
            else if (account is AzureServicePrincipalAccount servicePrincipalAccount)
            {
                throw new Exception($"Cloud service targets cannot use Service Principal accounts, a Management Certificate account is required.");
            }

            return 0;
        }
    }
}