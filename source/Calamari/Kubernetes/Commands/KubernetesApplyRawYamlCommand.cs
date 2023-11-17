using System.Threading.Tasks;
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
    public class KubernetesApplyRawYamlCommand : KubernetesDeploymentCommandBase
    {
        public const string Name = "kubernetes-apply-raw-yaml";

        private readonly IVariables variables;
        private readonly IResourceStatusReportExecutor statusReporter;
        private readonly IGatherAndApplyRawYamlExecutor gatherAndApplyRawYamlExecutor;

        public KubernetesApplyRawYamlCommand(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IGatherAndApplyRawYamlExecutor gatherAndApplyRawYamlExecutor,
            IResourceStatusReportExecutor statusReporter,
            Kubectl kubectl)
            : base(log, deploymentJournalWriter, variables, fileSystem, extractPackage,
            substituteInFiles, structuredConfigVariablesService, kubectl)
        {
            this.variables = variables;
            this.statusReporter = statusReporter;
            this.gatherAndApplyRawYamlExecutor = gatherAndApplyRawYamlExecutor;
        }

        protected override async Task<bool> ExecuteCommand(RunningDeployment runningDeployment)
        {
            if (!variables.GetFlag(SpecialVariables.ResourceStatusCheck))
            {
                return await gatherAndApplyRawYamlExecutor.Execute(runningDeployment);
            }

            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);
            
            var statusCheck = statusReporter.Start(timeoutSeconds, waitForJobs);

            return await gatherAndApplyRawYamlExecutor.Execute(runningDeployment, statusCheck.AddResources) &&
                await statusCheck.WaitForCompletionOrTimeout();
        }
    }
}