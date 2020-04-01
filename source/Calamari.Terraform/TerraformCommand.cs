using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;

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

        public int Execute(string[] args)
        {
            var pathToPrimaryPackage = variables.GetPathToPrimaryPackage(fileSystem, false);
            
            var isEnableNoMatchWarningSet = variables.IsSet(SpecialVariables.Package.EnableNoMatchWarning);
            if (!isEnableNoMatchWarningSet && !string.IsNullOrEmpty(GetAdditionalFileSubstitutions()))
                variables.Add(SpecialVariables.Package.EnableNoMatchWarning, "true");
            
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
                environmentVariables.MergeDictionaries(awsEnvironmentGeneration.EnvironmentVars);
            }

            if (useAzureAccount)
            {
                environmentVariables.MergeDictionaries(AzureEnvironmentVariables(variables));
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