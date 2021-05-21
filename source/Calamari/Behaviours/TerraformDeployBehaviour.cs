using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Terraform.Behaviours
{
    abstract class TerraformDeployBehaviour : IDeployBehaviour
    {
        protected readonly ILog log;

        protected TerraformDeployBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;
            var environmentVariables = new Dictionary<string, string>();
            var useAWSAccount = variables.Get(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "None") == "AWS";
            var useAzureAccount = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount);
            var useGoogleCloudAccount = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.GoogleCloudAccount);

            if (useAWSAccount)
            {
                var awsEnvironmentGeneration = await AwsEnvironmentGeneration.Create(log, variables).ConfigureAwait(false);
                environmentVariables.AddRange(awsEnvironmentGeneration.EnvironmentVars);
            }

            if (useAzureAccount)
            {
                environmentVariables.AddRange(AzureEnvironmentVariables(variables));
            }

            if (useGoogleCloudAccount)
            {
                environmentVariables.AddRange(GoogleCloudEnvironmentVariables(variables));
            }

            environmentVariables.AddRange(GetEnvironmentVariableArgs(variables));

            await Execute(context, environmentVariables);
        }

        static Dictionary<string, string> GetEnvironmentVariableArgs(IVariables variables)
        {
            var rawJson = variables.Get(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables);
            if (string.IsNullOrEmpty(rawJson))
                return new Dictionary<string, string>();

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(rawJson);
        }

        protected abstract Task Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables);

        Dictionary<string, string> GoogleCloudEnvironmentVariables(IVariables variables)
        {
            var account = variables.Get("Octopus.Action.GoogleCloudAccount.Variable")?.Trim();
            var keyFile = variables.Get($"{account}.JsonKey")?.Trim() ?? variables.Get("Octopus.Action.GoogleCloudAccount.JsonKey")?.Trim();

            var bytes = Convert.FromBase64String(keyFile);
            var json = Encoding.UTF8.GetString(bytes);

            // See https://registry.terraform.io/providers/hashicorp/google/latest/docs/guides/provider_reference#full-reference
            return new Dictionary<string, string>
            {
                {"GOOGLE_CLOUD_KEYFILE_JSON", json}
            };
        }

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

            var environmentName = AzureEnvironment(variables.Get(AzureAccountVariables.Environment));

            var account = variables.Get(AzureAccountVariables.AccountVariable)?.Trim();
            var subscriptionId = variables.Get($"{account}.SubscriptionNumber")?.Trim() ?? variables.Get(AzureAccountVariables.SubscriptionId)?.Trim();
            var clientId = variables.Get($"{account}.Client")?.Trim() ?? variables.Get(AzureAccountVariables.ClientId)?.Trim();
            var clientSecret = variables.Get($"{account}.Password")?.Trim() ?? variables.Get(AzureAccountVariables.Password)?.Trim();
            var tenantId = variables.Get($"{account}.TenantId")?.Trim() ?? variables.Get(AzureAccountVariables.TenantId)?.Trim();

            var env = new Dictionary<string, string>
            {
                { "ARM_SUBSCRIPTION_ID", subscriptionId },
                { "ARM_CLIENT_ID", clientId },
                { "ARM_CLIENT_SECRET", clientSecret },
                { "ARM_TENANT_ID", tenantId },
                { "ARM_ENVIRONMENT", environmentName }
            };

            return env;
        }
    }
}