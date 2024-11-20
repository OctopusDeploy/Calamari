using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Aws.Integration;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Conventions.DependencyVariables;
using Calamari.Kubernetes.Conventions;
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Kubernetes.Commands
{
    [Command("helm-upgrade", Description = "Performs Helm Upgrade with Chart while performing variable replacement")]
    public class HelmUpgradeCommand : Command
    {
        PathToPackage pathToPackage;
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;
        readonly HelmTemplateValueSourcesParser templateValueSourcesParser;
        readonly IResourceStatusReportExecutor statusExecutor;
        readonly ICommandLineRunner commandLineRunner;
        readonly IManifestReporter manifestReporter;
        readonly Kubectl kubectl;

        public HelmUpgradeCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            ICalamariFileSystem fileSystem,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage,
            HelmTemplateValueSourcesParser templateValueSourcesParser,
            IResourceStatusReportExecutor statusExecutor,
            IManifestReporter manifestReporter,
            Kubectl kubectl)
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
            this.templateValueSourcesParser = templateValueSourcesParser;
            this.statusExecutor = statusExecutor;
            this.manifestReporter = manifestReporter;
            this.kubectl = kubectl;
            this.commandLineRunner = commandLineRunner;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (!File.Exists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage))
            };

            if (OctopusFeatureToggles.NonPrimaryGitDependencySupportFeatureToggle.IsEnabled(variables))
            {
                conventions.Add(new StageDependenciesConvention(null,
                                                                fileSystem,
                                                                new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner),
                                                                new PackageVariablesFactory(),
                                                                true));
                conventions.Add(new StageDependenciesConvention(null,
                                                                fileSystem,
                                                                new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner),
                                                                new GitDependencyVariablesFactory(),
                                                                true));
            }
            else
            {
                conventions.Add(new StageScriptPackagesConvention(null, fileSystem, new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner), true));
            }

            conventions.AddRange(new IInstallConvention[]
            {
                new ConfiguredScriptConvention(new PreDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                // Any values.yaml files in any packages referenced by the step will automatically have variable substitution applied (we won't log a warning if these aren't present)
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, DefaultValuesFiles().ToList(), false)),
                // Any values file explicitly specified by the user will also have variable substitution applied
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, ExplicitlySpecifiedValuesFiles().ToList(), true)),
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, TemplateValuesFiles(d), true)),
                new ConfiguredScriptConvention(new DeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
            });
            
            conventions.AddRange(AddHelmUpgradeConventions());
            
            conventions.Add(new ConfiguredScriptConvention(new PostDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)));

            var deployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(deployment, conventions, log);
            conventionRunner.RunConventions();

            return 0;
        }

        IInstallConvention[] AddHelmUpgradeConventions()
        {
            //if the feature toggle _is_ enabled, use different conventions (as the HelmCli doesn't use the script authentication)
            if (OctopusFeatureToggles.KOSForHelmFeatureToggle.IsEnabled(variables))
            {
                return new IInstallConvention[]
                {
                    new DelegateInstallConvention(d =>
                                                  {
                                                      //make sure the kubectl tool is configured correctly
                                                      kubectl.SetWorkingDirectory(d.CurrentDirectory);
                                                      kubectl.SetEnvironmentVariables(d.EnvironmentVariables);
                                                  }),
                    new ConditionalInstallationConvention<AwsAuthConvention>(runningDeployment => runningDeployment.Variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount",
                                                                             new AwsAuthConvention(log, variables)),

                    new KubernetesAuthContextConvention(log, new CommandLineRunner(log, variables), kubectl, fileSystem),

                    new HelmUpgradeWithKOSConvention(log,
                                                     commandLineRunner,
                                                     fileSystem,
                                                     templateValueSourcesParser,
                                                     statusExecutor,
                                                     manifestReporter,
                                                     kubectl)
                };
            }

            //if the feature toggle _is not_ enabled, use the old convention
            return new IInstallConvention[]
            {
                new HelmUpgradeConvention(
                                          log,
                                          scriptEngine,
                                          commandLineRunner,
                                          fileSystem,
                                          templateValueSourcesParser)
            };
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
                    yield return Path.Combine(PackageDirectory(prn), path);
                }
            }
        }

        //we don't log the included files as they will have been done separately
        IList<string> TemplateValuesFiles(RunningDeployment deployment) => templateValueSourcesParser.ParseTemplateValuesFilesFromDependencies(deployment, false).ToList();

        string PackageDirectory(string packageReferenceName)
        {
            var packageRoot = packageReferenceName;
            if (string.IsNullOrEmpty(packageReferenceName))
            {
                packageRoot = PackageName.ExtractPackageNameFromPathedPackageId(variables.Get(PackageVariables.IndexedPackageId(packageReferenceName ?? "")));
            }

            return fileSystem.RemoveInvalidFileNameChars(packageRoot ?? string.Empty);
        }
    }
}