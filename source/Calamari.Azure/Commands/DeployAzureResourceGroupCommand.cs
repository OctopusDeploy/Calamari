﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Variables;
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
            var packagedScriptService = new PackagedScriptService(log, fileSystem, scriptEngine, commandLineRunner);
            var configuredScriptService = new ConfiguredScriptService(fileSystem, scriptEngine, commandLineRunner);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PreDeploy),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PreDeploy),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.Deploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.Deploy),
                new DeployAzureResourceGroupConvention(templateFile, templateParameterFile, filesInPackage, templateService, new ResourceGroupTemplateNormalizer()),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PostDeploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PostDeploy),
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }
    }
}