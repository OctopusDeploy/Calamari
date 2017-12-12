using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;

namespace Calamari.Aws.Deployment.Conventions
{
    public class DeployAwsCloudFormationConvention : IInstallConvention
    {
        private static readonly ITemplateReplacement TemplateReplacement = new TemplateReplacement();
        
        readonly string templateFile;
        readonly string templateParametersFile;
        private readonly bool filesInPackage;
        readonly ICalamariFileSystem fileSystem;

        public DeployAwsCloudFormationConvention(
            string templateFile, 
            string templateParametersFile, 
            bool filesInPackage, 
            ICalamariFileSystem fileSystem)
        {
            this.templateFile = templateFile;
            this.templateParametersFile = templateParametersFile;
            this.filesInPackage = filesInPackage;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var stackName = variables[SpecialVariables.Action.Aws.CloudFormationStackName];
 
            var template = TemplateReplacement.ResolveAndSubstituteFile(fileSystem, templateFile, filesInPackage, variables); 
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile) 
                ? TemplateReplacement.ResolveAndSubstituteFile(fileSystem, templateParametersFile, filesInPackage, variables)
                : null;
        }
    }
}