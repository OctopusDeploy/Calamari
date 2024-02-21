using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Conventions;

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

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new StageScriptPackagesConvention(null, fileSystem, new CombinedPackageExtractor(log, variables, commandLineRunner), true),
                new ConfiguredScriptConvention(new PreDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                // Any values.yaml files in any packages referenced by the step will automatically have variable substitution applied (we won't log a warning if these aren't present)
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, DefaultValuesFiles().ToList(), false)),
                // Any values files explicitly specified by the user will also have variable substitution applied
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, ExplicitlySpecifiedValuesFiles().ToList(), true)),
                new ConfiguredScriptConvention(new DeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new HelmUpgradeConvention(log, scriptEngine, commandLineRunner, fileSystem),
                new ConfiguredScriptConvention(new PostDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner))
            };
            var deployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(deployment, conventions, log);
            conventionRunner.RunConventions();
            
            return 0;
        }

        /// <summary>
        /// Any values.yaml files in any packages referenced by the step will automatically have variable substitution applied
        /// </summary>
        IEnumerable<string> DefaultValuesFiles()
        {
            var packageReferenceNames = variables.GetIndexes(PackageVariables.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var prn = PackageName.ExtractPackageNameFromPathedPackageId(packageReferenceName);
                yield return Path.Combine(PackageDirectory(prn), "values.yaml");
            }
        }

        /// <summary>
        /// Any values files explicitly specified by the user will also have variable substitution applied
        /// </summary>
        IEnumerable<string> ExplicitlySpecifiedValuesFiles()
        {
            var packageReferenceNames = variables.GetIndexes(PackageVariables.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var prn = PackageName.ExtractPackageNameFromPathedPackageId(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(prn));

                foreach (var path in paths)
                {
                    yield return Path.Combine(PackageDirectory(packageReferenceName), path);
                }
            }
        }

        string PackageDirectory(string packageReferenceName)
        {
            var packageRoot = packageReferenceName;
            if (string.IsNullOrEmpty(packageReferenceName))
            {
                packageRoot = variables.Get(PackageVariables.IndexedPackageId(packageReferenceName ?? ""));
            }
            return fileSystem.RemoveInvalidFileNameChars(packageRoot ?? string.Empty);
        }
    }
}