using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
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
    [Command(Name, Description = "Write populated template from a package into git repositories")]
    public class CommitToGitCommand : Command
    {
        public const string PackageDirectoryName = "package";

        public const string Name = "commit-to-git";

        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IExtractPackage extractPackage;
        readonly ISubstituteInFiles substituteInFiles;
        PathToPackage pathToPackage;

        public CommitToGitCommand(
            ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            this.substituteInFiles = substituteInFiles;

            Options.Add("package=",
                        "Path to the NuGet package to install.",
                        v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var workingDirectory = Directory.GetCurrentDirectory();

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d =>
                                              {
                                                  workingDirectory = d.CurrentDirectory;
                                                  var stagingDirectory = Path.Combine(workingDirectory, ExtractPackage.StagingDirectoryName);
                                                  var packageDirectory = Path.Combine(stagingDirectory, PackageDirectoryName);
                                                  fileSystem.EnsureDirectoryExists(packageDirectory);
                                                  extractPackage.ExtractToCustomDirectory(pathToPackage, packageDirectory);

                                                  d.StagingDirectory = packageDirectory;
                                                  d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                              }),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles, PackageDirectoryName)),
                new DelegateInstallConvention(d =>
                                              {
                                                  var convention = new UpdateGitRepositoryInstallConvention(fileSystem, workingDirectory, log);
                                                  convention.Install(d);
                                              })
                
            };

            var runningDeployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);

            //need to work out if the conventions somehow fails
            return 1;
        }
    }
}