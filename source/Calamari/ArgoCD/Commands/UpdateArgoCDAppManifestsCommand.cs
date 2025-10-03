#if NET
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.GitHub;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Commands
{
    [Command(Name, Description = "Update Kubernetes manifests from a package for one or more Argo CD Applications, persisting them in a Git repository")]
    public class UpdateArgoCDAppManifestsCommand : Command
    {
        public const string PackageDirectoryName = "package";

        public const string Name = "update-argo-cd-app-manifests";

        readonly ILog log;
        readonly IVariables variables;
        readonly INonSensitiveVariables nonSensitiveVariables;
        readonly ICalamariFileSystem fileSystem;
        readonly IExtractPackage extractPackage;
        readonly INonSensitiveSubstituteInFiles substituteInFiles;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly DeploymentConfigFactory configFactory;
        PathToPackage pathToPackage;
        string customPropertiesFile;
        string customPropertiesPassword;

        public UpdateArgoCDAppManifestsCommand(
            ILog log,
            IVariables variables,
            INonSensitiveVariables nonSensitiveVariables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            INonSensitiveSubstituteInFiles substituteInFiles,
            IGitHubPullRequestCreator pullRequestCreator,
            DeploymentConfigFactory configFactory)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            this.substituteInFiles = substituteInFiles;
            this.pullRequestCreator = pullRequestCreator;
            this.configFactory = configFactory;
            this.nonSensitiveVariables = nonSensitiveVariables;

            Options.Add("package=",
                        "Path to the NuGet package to install.",
                        v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
            Options.Add("customPropertiesFile=",
                        "Name of the custom properties file",
                        v => customPropertiesFile = Path.GetFullPath(v));
            Options.Add("customPropertiesPassword=",
                        "Password to decrypt the custom properties file",
                        v => customPropertiesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d =>
                                              {
                                                  var workingDirectory = d.CurrentDirectory;
                                                  var packageDirectory = Path.Combine(workingDirectory, PackageDirectoryName);
                                                  fileSystem.EnsureDirectoryExists(packageDirectory);
                                                  extractPackage.ExtractToCustomDirectory(pathToPackage, packageDirectory);
                                                  
                                                  d.StagingDirectory = workingDirectory;
                                                  d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                              }),
                new SubstituteInFilesConvention(new NonSensitiveSubstituteInFilesBehaviour(substituteInFiles, PackageDirectoryName)
                {
                    //we don't want to log a warning if files are not found
                    WarnIfFilesNotFound = false
                }),
                
                new UpdateArgoCDApplicationManifestsInstallConvention(fileSystem, PackageDirectoryName, log, pullRequestCreator, configFactory, new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword), new ArgoCdApplicationManifestParser()),
            };

            var runningDeployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);
            
            return 0;
        }
    }
}
#endif