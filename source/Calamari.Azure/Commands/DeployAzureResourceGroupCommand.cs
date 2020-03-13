using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Util;

namespace Calamari.Azure.Commands
{
    [Command("deploy-azure-resource-group", Description = "Creates a new Azure Resource Group deployment")]
    public class DeployAzureResourceGroupCommand : Command
    {
        private readonly CombinedScriptEngine scriptEngine;
        readonly IVariables variables;
        private string packageFile;
        private string templateFile;
        private string templateParameterFile;

        public DeployAzureResourceGroupCommand(CombinedScriptEngine scriptEngine, IVariables variables)
        {
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("template=", "Path to the JSON template file.", v => templateFile = v);
            Options.Add("templateParameters=", "Path to the JSON template parameters file.", v => templateParameterFile = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, Environment.CurrentDirectory);
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var fileSystem = new WindowsPhysicalFileSystem();
            var filesInPackage = !string.IsNullOrWhiteSpace(packageFile);
            var templateResolver = new TemplateResolver(fileSystem);
            var templateService = new TemplateService(fileSystem, templateResolver, new TemplateReplacement(templateResolver));
            
            var conventions = new List<IConvention>
            {
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new DeployAzureResourceGroupConvention(templateFile, templateParameterFile, filesInPackage, templateService, new ResourceGroupTemplateNormalizer()),
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }

    }
}
