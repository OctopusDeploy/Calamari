using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Aws.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Octostache;

namespace Calamari.Terraform
{
    public abstract class TerraformConvention : IInstallConvention
    {
        protected readonly ICalamariFileSystem fileSystem;
        private readonly IFileSubstituter fileSubstituter;
        const string DefaultTerraformFileSubstitution = "**/*.tf\n**/*.tf.json\n**/*.tfvars\n**/*.tfvars.json";

        public TerraformConvention(ICalamariFileSystem fileSystem,  IFileSubstituter fileSubstituter)
        {
            this.fileSystem = fileSystem;
            this.fileSubstituter = fileSubstituter;
        }

        protected abstract void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables);

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        async Task InstallAsync(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var additionalFileSubstitution = variables.Get(TerraformSpecialVariables.Action.Terraform.FileSubstitution);
            var enableNoMatchWarning = variables.Get(SpecialVariables.Package.EnableNoMatchWarning);

            variables.Add(SpecialVariables.Package.EnableNoMatchWarning,
                !String.IsNullOrEmpty(enableNoMatchWarning) ? enableNoMatchWarning : (!String.IsNullOrEmpty(additionalFileSubstitution)).ToString());

            var substitutionPatterns = (DefaultTerraformFileSubstitution +
                                        (string.IsNullOrWhiteSpace(additionalFileSubstitution)
                                            ? String.Empty
                                            : "\n" + additionalFileSubstitution))
                .Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);

            new SubstituteInFilesConvention(fileSystem, fileSubstituter,
                    _ => true,
                    _ => substitutionPatterns)
                .Install(deployment);

            var environmentVariables = new Dictionary<string, string>();
            var useAWSAccount = variables.Get(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "None") == "AWS";
            var useAzureAccount = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount);

            if (useAWSAccount)
            {
                var awsEnvironmentGeneration = await AwsEnvironmentGeneration.Create(variables).ConfigureAwait(false);
                environmentVariables.MergeDictionaries(awsEnvironmentGeneration.EnvironmentVars);
            }

            if (useAzureAccount)
            {
                environmentVariables.MergeDictionaries(AzureEnvironmentVariables(variables));
            }

            Execute(deployment, environmentVariables);
        }

        // We not referencing the Azure project because we want to be multi-platform 
        static Dictionary<string, string> AzureEnvironmentVariables(VariableDictionary variables)
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