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
    public abstract class TerraformDeployBehaviour : IDeployBehaviour
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

        static Dictionary<string, string> GoogleCloudEnvironmentVariables(IVariables variables)
        {
            // See https://registry.terraform.io/providers/hashicorp/google/latest/docs/guides/provider_reference#full-reference
            var googleCloudEnvironmentVariables = new Dictionary<string, string>();
            var useVmServiceAccount = variables.GetFlag("Octopus.Action.GoogleCloud.UseVMServiceAccount");
            var account = variables.Get("Octopus.Action.GoogleCloudAccount.Variable")?.Trim();
            var keyFile = variables.Get($"{account}.JsonKey")?.Trim() ?? variables.Get("Octopus.Action.GoogleCloudAccount.JsonKey")?.Trim();

            if (!useVmServiceAccount && !string.IsNullOrEmpty(keyFile))
            {
                var bytes = Convert.FromBase64String(keyFile);
                var json = Encoding.UTF8.GetString(bytes);
                googleCloudEnvironmentVariables.Add("GOOGLE_CLOUD_KEYFILE_JSON", json);
                Log.Verbose($"A JSON key has been set to GOOGLE_CLOUD_KEYFILE_JSON environment variable");
            }

            var impersonateServiceAccount = variables.GetFlag("Octopus.Action.GoogleCloud.ImpersonateServiceAccount");
            if (impersonateServiceAccount)
            {
                var serviceAccountEmails = variables.Get("Octopus.Action.GoogleCloud.ServiceAccountEmails") ?? string.Empty;
                googleCloudEnvironmentVariables.Add("GOOGLE_IMPERSONATE_SERVICE_ACCOUNT", serviceAccountEmails);
                Log.Verbose($"{serviceAccountEmails} has been set to GOOGLE_IMPERSONATE_SERVICE_ACCOUNT environment variable");
            }

            var project = variables.Get("Octopus.Action.GoogleCloud.Project")?.Trim();
            var region = variables.Get("Octopus.Action.GoogleCloud.Region")?.Trim();
            var zone = variables.Get("Octopus.Action.GoogleCloud.Zone")?.Trim();

            if (!string.IsNullOrEmpty(project))
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_PROJECT", project);
                Log.Verbose($"{project} has been set to GOOGLE_PROJECT environment variable");
            }
            
            if (!string.IsNullOrEmpty(region))
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_REGION", region);
                Log.Verbose($"{region} has been set to GOOGLE_REGION environment variable");
            }
            
            if (!string.IsNullOrEmpty(zone))
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_ZONE", zone);
                Log.Verbose($"{zone} has been set to GOOGLE_ZONE environment variable");
            }
            
            return googleCloudEnvironmentVariables;
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
            var oidcToken = variables.Get($"{account}.OpenIdConnect.Jwt")?.Trim() ?? variables.Get(AzureAccountVariables.OpenIDJwt)?.Trim();
            var tenantId = variables.Get($"{account}.TenantId")?.Trim() ?? variables.Get(AzureAccountVariables.TenantId)?.Trim();

            var env = new Dictionary<string, string>
            {
                { "ARM_SUBSCRIPTION_ID", subscriptionId },
                { "ARM_CLIENT_ID", clientId },
                { "ARM_TENANT_ID", tenantId },
                { "ARM_ENVIRONMENT", environmentName }
            };

            if (!string.IsNullOrEmpty(oidcToken))
            {
                // https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/guides/service_principal_oidc
                // Docs mention support for Oidc accounts from 3.7.0 this fails for service principal accounts,
                // but is fixed in 3.22 (This is to be mentioned in docs)
                // These versions can be supported by running the "az login ... --federated-token" command from the cli
                // We stick to the terraform auth to avoid polluting the local az cli login context.
                // Additional issues 
                // https://github.com/hashicorp/terraform-provider-azurerm/issues/22034
                // https://github.com/hashicorp/terraform-provider-azurerm/issues/20546
                env.Add("ARM_OIDC_TOKEN", oidcToken);
                env.Add("ARM_USE_OIDC", "true");
                
            }
            else
            {
                env.Add("ARM_CLIENT_SECRET", clientSecret);
            }

            return env;
        }
    }
}