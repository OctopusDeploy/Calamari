using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Terraform
{
    public abstract class TerraformCommand : ICommand
    {
        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;

        protected TerraformCommand(
            ILog log, 
            IVariables variables, 
            ICalamariFileSystem fileSystem, 
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage
            )
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
        }

        public int Execute()
        {
            var pathToPrimaryPackage = variables.GetPathToPrimaryPackage(fileSystem, false);
            
            var isEnableNoMatchWarningSet = variables.IsSet(PackageVariables.EnableNoMatchWarning);
            if (!isEnableNoMatchWarningSet)
            {
                var hasAdditionalSubstitutions = !string.IsNullOrEmpty(GetAdditionalFileSubstitutions()); 
                variables.AddFlag(PackageVariables.EnableNoMatchWarning, hasAdditionalSubstitutions);
            }

            var runningDeployment = new RunningDeployment(pathToPrimaryPackage, variables);
            
            if(pathToPrimaryPackage != null)
                extractPackage.ExtractToStagingDirectory(pathToPrimaryPackage);

            var filesToSubstitute = GetFilesToSubstitute();
            substituteInFiles.Substitute(runningDeployment, filesToSubstitute);
            
            InstallAsync(runningDeployment).GetAwaiter().GetResult();
            
            return 0;
        }

        string[] GetFilesToSubstitute()
        {
            var result = new List<string>();

            var runAutomaticFileSubstitution = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.RunAutomaticFileSubstitution, true);
            if (runAutomaticFileSubstitution)
                result.AddRange(new[] {"**/*.tf", "**/*.tf.json", "**/*.tfvars", "**/*.tfvars.json"});

            var additionalFileSubstitution = GetAdditionalFileSubstitutions();
            if (!string.IsNullOrWhiteSpace(additionalFileSubstitution))
                result.AddRange(additionalFileSubstitution.Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries));

            return result.ToArray();
        }

        string GetAdditionalFileSubstitutions()
            => variables.Get(TerraformSpecialVariables.Action.Terraform.FileSubstitution);
        
        
        async Task InstallAsync(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var environmentVariables = new Dictionary<string, string>();
            var useAWSAccount = variables.Get(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "None") == "AWS";
            var useAzureAccount = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount);

            if (useAWSAccount)
            {
                var awsEnvironmentGeneration = await AwsEnvironmentGeneration.Create(log, variables).ConfigureAwait(false);
                environmentVariables.AddRange(awsEnvironmentGeneration.EnvironmentVars);
            }

            if (useAzureAccount)
            {
                environmentVariables.AddRange(AzureEnvironmentVariables(variables));
            }

            Execute(deployment, environmentVariables);
        }
        
        protected abstract void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables);

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

            var environmentName = AzureEnvironment(variables.Get(AzureAccountVariables.Environment));
            
            var account = variables.Get(AzureAccountVariables.AccountVariable)?.Trim();
            var subscriptionId = variables.Get($"{account}.SubscriptionNumber")?.Trim() ?? variables.Get(AzureAccountVariables.SubscriptionId)?.Trim();
            var clientId = variables.Get($"{account}.Client")?.Trim() ?? variables.Get(AzureAccountVariables.ClientId)?.Trim();
            var clientSecret = variables.Get($"{account}.Password")?.Trim() ?? variables.Get(AzureAccountVariables.Password)?.Trim();
            var tenantId = variables.Get($"{account}.TenantId")?.Trim() ?? variables.Get(AzureAccountVariables.TenantId)?.Trim();
            
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