using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.Certificates;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Features.StructuredVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Kubernetes.Conventions;
using Octostache;

namespace Calamari.Kubernetes.Commands
{
    [Command("helm-upgrade", Description = "Performs Helm Upgrade with Chart while performing variable replacement")]
    public class HelmUpgradeCommand : Command
    {
        PathToPackage pathToPackage;
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly IDeploymentJournalWriter deploymentJournalWriter;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;
        readonly ICommandLineRunner commandLineRunner;

        public HelmUpgradeCommand(
            ILog log, 
            IScriptEngine scriptEngine, 
            IDeploymentJournalWriter deploymentJournalWriter, 
            IVariables variables,
			ICommandLineRunner commandLineRunner,
            ICalamariFileSystem fileSystem,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage
            )
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
            this.commandLineRunner = commandLineRunner;
        }
        
        public override int Execute(string[] commandLineArguments)
        {
              Options.Parse(commandLineArguments);

            if (!File.Exists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);
            
            ValidateRequiredVariables();
            
            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new StageScriptPackagesConvention(null, fileSystem, new CombinedPackageExtractor(log), true),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d, FileTargetFactory().ToList())),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new HelmUpgradeConvention(log, scriptEngine, commandLineRunner, fileSystem),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
            };
            var deployment = new RunningDeployment(pathToPackage, variables);
            
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);
            try
            {
                conventionRunner.RunConventions();
                deploymentJournalWriter.AddJournalEntry(deployment, true, pathToPackage);
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(deployment, false, pathToPackage);
                throw;
            }

            return 0;
        }

        private void ValidateRequiredVariables()
        {
            if (!variables.IsSet(SpecialVariables.ClusterUrl))
            {
                throw new CommandException($"The variable `{SpecialVariables.ClusterUrl}` is not provided.");
            }
        }
        
        private IEnumerable<string> FileTargetFactory()
        {
            var packageReferenceNames = variables.GetIndexes(PackageVariables.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(packageReferenceName));
                
                foreach (var path in paths)
                {
                    yield return Path.Combine(sanitizedPackageReferenceName, path);    
                }
            }
        }
    }
}