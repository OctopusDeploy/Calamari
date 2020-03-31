using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Aws.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Octostache;

namespace Calamari.Terraform
{
    public abstract class TerraformConvention : IInstallConvention
    {
        protected readonly ILog Log;
        protected readonly ICalamariFileSystem fileSystem;

        public TerraformConvention(ILog log, ICalamariFileSystem fileSystem)
        {
            this.Log = log;
            this.fileSystem = fileSystem;
        }

        protected abstract void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables);

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        async Task InstallAsync(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var environmentVariables = new Dictionary<string, string>();
            var useAWSAccount = variables.Get(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "None") == "AWS";
            var useAzureAccount = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount);

            if (useAWSAccount)
            {
                var awsEnvironmentGeneration = await AwsEnvironmentGeneration.Create(Log, variables).ConfigureAwait(false);
                environmentVariables.MergeDictionaries(awsEnvironmentGeneration.EnvironmentVars);
            }

            if (useAzureAccount)
            {
                environmentVariables.MergeDictionaries(AzureEnvironmentVariables(variables));
            }

            Execute(deployment, environmentVariables);
        }

        // We not referencing the Azure project because we want to be multi-platform 
        static Dictionary<string, string> AzureEnvironmentVariables(IVariables variables)
        {
            string AzureEnvironment(string s)
            {
                switch (s)
                {
                    case "AzureChinaCloud":
                        return "china";
                    case "AzureGermanCloud":
                        return "german";
                    case "AzureUSGovernment":
                        return "usgovernment";
                    default:
                        return "public";
                }
            }

            var environmentName = AzureEnvironment(variables.Get(SpecialVariables.Action.Azure.Environment));

            var account = variables.Get(SpecialVariables.Action.Azure.AccountVariable)?.Trim();
            var subscriptionId = variables.Get($"{account}.SubscriptionNumber")?.Trim() ?? variables.Get(SpecialVariables.Action.Azure.SubscriptionId)?.Trim();
            var clientId = variables.Get($"{account}.Client")?.Trim() ?? variables.Get(SpecialVariables.Action.Azure.ClientId)?.Trim();
            var clientSecret = variables.Get($"{account}.Password")?.Trim() ?? variables.Get(SpecialVariables.Action.Azure.Password)?.Trim();
            var tenantId = variables.Get($"{account}.TenantId")?.Trim() ?? variables.Get(SpecialVariables.Action.Azure.TenantId)?.Trim();
            
            var env = new Dictionary<string, string>
            {
                {"ARM_SUBSCRIPTION_ID", subscriptionId},
                {"ARM_CLIENT_ID", clientId},
                {"ARM_CLIENT_SECRET", clientSecret},
                {"ARM_TENANT_ID", tenantId},
                {"ARM_ENVIRONMENT", environmentName}
            };

            return env;
        }
    }
}