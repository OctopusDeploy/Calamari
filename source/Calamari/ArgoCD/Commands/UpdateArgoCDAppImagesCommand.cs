#if NET
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Commands
{
    [Command(Name, Description = "Write populated templates from a package into one or more git repositories")]
    public class UpdateArgoCDAppImagesCommand : Command
    {
        public const string Name = "update-argo-cd-app-images";
        
        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly DeploymentConfigFactory configFactory;
        readonly ICommitMessageGenerator commitMessageGenerator;
        string customPropertiesFile;
        string customPropertiesPassword;

        public UpdateArgoCDAppImagesCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, IGitHubPullRequestCreator pullRequestCreator,
                                            ICommitMessageGenerator commitMessageGenerator,
                                            DeploymentConfigFactory configFactory)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.pullRequestCreator = pullRequestCreator;
            this.commitMessageGenerator = commitMessageGenerator;
            this.configFactory = configFactory;
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
            var runningDeployment = new RunningDeployment(null, variables);

            var conventions = new List<IConvention>
            {
                new UpdateArgoCDAppImagesInstallConvention(log, pullRequestCreator, fileSystem, configFactory, commitMessageGenerator, new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword), new ArgoCdApplicationManifestParser()),
            };
                
            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);
            
            return 0;
        }
    }
}

#endif