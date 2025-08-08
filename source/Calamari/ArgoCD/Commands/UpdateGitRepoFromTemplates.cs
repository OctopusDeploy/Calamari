using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.ArgoCD.Commands
{
    
    [Command(Name, Description = "Apply Raw Yaml to Kubernetes Cluster")]
    public class UpdateGitRepoFromTemplates : GitDeploymentBaseCommand
    {
        public const string Name = "update-git-repo-from-templates";

        readonly IVariables variables;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly UpdateGitFromTemplatesExecutor updateGitFromTemplatesExecutor;

        public UpdateGitRepoFromTemplates(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IResourceStatusReportExecutor statusReporter,
            UpdateGitFromTemplatesExecutor updateGitFromTemplatesExecutor)
            : base(log,
                   deploymentJournalWriter,
                   variables,
                   fileSystem,
                   extractPackage,
                   substituteInFiles,
                   structuredConfigVariablesService)
        {
            this.variables = variables;
            this.statusReporter = statusReporter;
            this.updateGitFromTemplatesExecutor = updateGitFromTemplatesExecutor;
        }

        protected override async Task<bool> ExecuteCommand(RunningDeployment runningDeployment)
        {
            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var statusCheck = statusReporter.Start(timeoutSeconds, waitForJobs);

            var pathToTemplates = Path.Combine(runningDeployment.StagingDirectory, PackageDirectoryName);
            
            return await updateGitFromTemplatesExecutor.Execute(runningDeployment, pathToTemplates) &&
                   await statusCheck.WaitForCompletionOrTimeout(CancellationToken.None);
        }
    }
}