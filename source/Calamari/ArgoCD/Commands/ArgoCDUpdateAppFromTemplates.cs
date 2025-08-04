using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Commands.Executors;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Apply Raw Yaml to Kubernetes Cluster")]
    public class ArgoCDUpdateAppFromTemplates : ArgoCDDeploymentBaseCommand
    {
        public const string Name = "kubernetes-apply-raw-yaml";

        readonly IVariables variables;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly ArgoCDTemplateExecutor argoCdTemplateExecutor;

        public ArgoCDUpdateAppFromTemplates(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IResourceStatusReportExecutor statusReporter)
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
        }

        protected override async Task<bool> ExecuteCommand(RunningDeployment runningDeployment)
        {
            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var statusCheck = statusReporter.Start(timeoutSeconds, waitForJobs);
            
            return await argoCdTemplateExecutor.Execute(runningDeployment, (newResources) => statusCheck.AddResources(newResources)) &&
                   await statusCheck.WaitForCompletionOrTimeout(CancellationToken.None);
        }
    }
}