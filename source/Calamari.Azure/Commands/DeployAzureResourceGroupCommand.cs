using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;

namespace Calamari.Azure.Commands
{
    [Command("deploy-azure-resource-group", Description = "Creates a new Azure Resource Group deployment")]
    public class DeployAzureResourceGroupCommand : Command
    {
        readonly ILog log;
        private readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;
        readonly IExtractPackage extractPackage;
        private PathToPackage pathToPackage;
        private string templateFile;
        private string templateParameterFile;

        public DeployAzureResourceGroupCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            IExtractPackage extractPackage
        )
        {
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
            this.extractPackage = extractPackage;
            Options.Add("package=", "Path to the deployment package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
            Options.Add("template=", "Path to the JSON template file.", v => templateFile = v);
            Options.Add("templateParameters=", "Path to the JSON template parameters file.", v => templateParameterFile = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            variables.Set(KnownVariables.OriginalPackageDirectoryPath, Environment.CurrentDirectory);
            var fileSystem = new WindowsPhysicalFileSystem();
            var filesInPackage = !string.IsNullOrWhiteSpace(pathToPackage);
            var templateResolver = new TemplateResolver(fileSystem);
            var templateService = new TemplateService(fileSystem, templateResolver, new TemplateReplacement(templateResolver));

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(log, DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(log, DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new DeployAzureResourceGroupConvention(templateFile, templateParameterFile, filesInPackage, templateService, new ResourceGroupTemplateNormalizer()),
                new PackagedScriptConvention(log, DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

            conventionRunner.RunConventions();
            return 0;
        }
    }
}