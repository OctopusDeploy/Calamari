using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Calamari.Aws.Integration;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public abstract class TerraformCommand : Command
    {
        const string DefaultTerraformFileSubstitution = "**/*.tf\n**/*.tf.json\n**/*.tfvars\n**/*.tfvars.json";

        readonly IVariables variables;
        protected readonly ICalamariFileSystem fileSystem;
        private string packageFile;


        protected TerraformCommand(IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            Options.Add("package=", "Path to the package to extract that contains the package.", v => packageFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (!string.IsNullOrEmpty(packageFile))
            {
                if (!fileSystem.FileExists(packageFile))
                {
                    throw new CommandException("Could not find package file: " + packageFile);
                }
            }

            var substituter = new FileSubstituter(fileSystem);
            var packageExtractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();
            var additionalFileSubstitution = variables.Get(TerraformSpecialVariables.Action.Terraform.FileSubstitution);
            var runAutomaticFileSubstitution = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.RunAutomaticFileSubstitution, true);
            var enableNoMatchWarning = variables.Get(SpecialVariables.Package.EnableNoMatchWarning);

            variables.Add(SpecialVariables.Package.EnableNoMatchWarning,
                !String.IsNullOrEmpty(enableNoMatchWarning) ? enableNoMatchWarning : (!String.IsNullOrEmpty(additionalFileSubstitution)).ToString());

            var conventions = new List<IConvention>
            {
                new ExtractPackageToStagingDirectoryConvention(packageExtractor, fileSystem).When(_ => packageFile != null),
                new SubstituteInFilesConvention(fileSystem, substituter,
                    _ => true,
                    _ => FileTargetFactory(runAutomaticFileSubstitution ? DefaultTerraformFileSubstitution : string.Empty, additionalFileSubstitution)),
                new DelegateInstallConvention(d => InstallAsync(d).GetAwaiter().GetResult())
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }


        static string[] FileTargetFactory(string defaultFileSubstitution, string additionalFileSubstitution)
        {
            return (defaultFileSubstitution +
                                        (string.IsNullOrWhiteSpace(additionalFileSubstitution)
                                            ? string.Empty
                                            : "\n" + additionalFileSubstitution))
                .Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
        }
        
        
        async Task InstallAsync(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
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