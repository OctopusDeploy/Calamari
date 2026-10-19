using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.GoogleCloudAccounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                var oauthFileConfiguration = new GcloudOAuthFileConfiguration(context.CurrentDirectory);
                environmentVariables.AddRange(GoogleCloudEnvironmentVariables(variables, oauthFileConfiguration));
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

        Dictionary<string, string> GoogleCloudEnvironmentVariables(IVariables variables, GcloudOAuthFileConfiguration oauthFileConfiguration)
        {
            // See https://registry.terraform.io/providers/hashicorp/google/latest/docs/guides/provider_reference#full-reference
            var googleCloudEnvironmentVariables = new Dictionary<string, string>();
            var useVmServiceAccount = variables.GetFlag("Octopus.Action.GoogleCloud.UseVMServiceAccount");
            var account = variables.Get("Octopus.Action.GoogleCloudAccount.Variable")?.Trim();
            var keyFile = variables.Get($"{account}.JsonKey")?.Trim() ?? variables.Get("Octopus.Action.GoogleCloudAccount.JsonKey")?.Trim();
            var jwtToken = variables.Get($"{account}.OpenIdConnect.Jwt");

            var impersonateServiceAccount = variables.GetFlag("Octopus.Action.GoogleCloud.ImpersonateServiceAccount");
            if (impersonateServiceAccount)
            {
                var serviceAccountEmails = variables.Get("Octopus.Action.GoogleCloud.ServiceAccountEmails") ?? string.Empty;
                googleCloudEnvironmentVariables.Add("GOOGLE_IMPERSONATE_SERVICE_ACCOUNT", serviceAccountEmails);
                log.Verbose($"{serviceAccountEmails} has been set to GOOGLE_IMPERSONATE_SERVICE_ACCOUNT environment variable");
            }

            void AddCredentials(string json, string description)
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_CLOUD_KEYFILE_JSON", json);
                log.Verbose($"Set GOOGLE_CLOUD_KEYFILE_JSON with {description}");

                // The gcs backend doesn't read GOOGLE_CLOUD_KEYFILE_JSON, only its own variable or GOOGLE_CREDENTIALS.
                // See https://developer.hashicorp.com/terraform/language/backend/gcs
                googleCloudEnvironmentVariables.Add("GOOGLE_BACKEND_CREDENTIALS", json);
                log.Verbose($"Set GOOGLE_BACKEND_CREDENTIALS with {description}");
            }

            if (!useVmServiceAccount)
            {
                if (!string.IsNullOrWhiteSpace(keyFile))
                {
                    var bytes = Convert.FromBase64String(keyFile);
                    var json = Encoding.UTF8.GetString(bytes);
                    var clientEmail = ReadJsonField(json, "client_email") ?? "an unknown service account";
                    AddCredentials(json, $"JSON key for {clientEmail} from account variable {account}");
                }
                else if (!string.IsNullOrWhiteSpace(jwtToken))
                {
                    // Gcloud Oidc accounts require the gcloud CLI to be installed and configured
                    var commandLineRunner = new CommandLineRunner(log, variables);
                    var setupGCloudAuthentication = new SetupGCloudAuthentication(variables,
                                                                                  log,
                                                                                  commandLineRunner,
                                                                                  googleCloudEnvironmentVariables,
                                                                                  oauthFileConfiguration);

                    var result = setupGCloudAuthentication.Execute();
                    if (result.ExitCode != 0)
                    {
                        throw new CommandException(result.Errors ?? "Could not authenticate using gcloud OpenId Connect");
                    }

                    var jsonAuthContent = File.ReadAllText(oauthFileConfiguration.JsonAuthFile.FilePath);
                    var serviceAccount = ServiceAccountFromImpersonationUrl(ReadJsonField(jsonAuthContent, "service_account_impersonation_url")) ?? "an unknown service account";
                    var audience = ReadJsonField(jsonAuthContent, "audience") ?? "an unknown audience";
                    AddCredentials(jsonAuthContent, $"OIDC credential configuration for {serviceAccount} from account variable {account} (audience {audience})");
                }
            }

            var project = variables.Get("Octopus.Action.GoogleCloud.Project")?.Trim();
            var region = variables.Get("Octopus.Action.GoogleCloud.Region")?.Trim();
            var zone = variables.Get("Octopus.Action.GoogleCloud.Zone")?.Trim();

            if (!string.IsNullOrEmpty(project))
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_PROJECT", project);
                log.Verbose($"{project} has been set to GOOGLE_PROJECT environment variable");
            }
            
            if (!string.IsNullOrEmpty(region))
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_REGION", region);
                log.Verbose($"{region} has been set to GOOGLE_REGION environment variable");
            }
            
            if (!string.IsNullOrEmpty(zone))
            {
                googleCloudEnvironmentVariables.Add("GOOGLE_ZONE", zone);
                log.Verbose($"{zone} has been set to GOOGLE_ZONE environment variable");
            }
            
            return googleCloudEnvironmentVariables;
        }

        static string ReadJsonField(string json, string field)
        {
            try
            {
                return JObject.Parse(json).Value<string>(field);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // e.g. https://iamcredentials.googleapis.com/v1/projects/-/serviceAccounts/deployer@project.iam.gserviceaccount.com:generateAccessToken
        static string ServiceAccountFromImpersonationUrl(string url)
        {
            var match = Regex.Match(url ?? string.Empty, "serviceAccounts/([^/:]+)");
            return match.Success ? match.Groups[1].Value : null;
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