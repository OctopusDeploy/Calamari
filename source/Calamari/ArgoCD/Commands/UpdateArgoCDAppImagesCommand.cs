using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.GitHub;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Time;

namespace Calamari.ArgoCD.Commands
{
    [Command(Name, Description = "Update container image versions for one or more Argo CD Applications, persisting them in a Git repository")]
    public class UpdateArgoCDAppImagesCommand : Command
    {
        public const string Name = "update-argo-cd-app-images";

        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly DeploymentConfigFactory configFactory;
        readonly IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory;
        readonly ICommitMessageGenerator commitMessageGenerator;
        string customPropertiesFile;
        string customPropertiesPassword;

        public UpdateArgoCDAppImagesCommand(ILog log,
                                            IVariables variables,
                                            ICalamariFileSystem fileSystem,
                                            ICommitMessageGenerator commitMessageGenerator,
                                            DeploymentConfigFactory configFactory,
                                            IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commitMessageGenerator = commitMessageGenerator;
            this.configFactory = configFactory;
            this.gitVendorAgnosticApiAdapterFactory = gitVendorAgnosticApiAdapterFactory;
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
            var clock = new SystemClock();
            var runningDeployment = new RunningDeployment(null, variables);

            var conventions = new List<IConvention>
            {
                new UpdateArgoCDAppImagesInstallConvention(log,
                                                           fileSystem,
                                                           configFactory,
                                                           commitMessageGenerator,
                                                           new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword),
                                                           new ArgoCdApplicationManifestParser(),
                                                           gitVendorAgnosticApiAdapterFactory,
                                                           clock,
                                                           new ArgoCDOutputVariablesWriter(log, variables)),
            };

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);

            return 0;
        }
    }
}