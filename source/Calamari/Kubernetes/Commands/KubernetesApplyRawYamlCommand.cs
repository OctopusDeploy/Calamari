#if !NET40
using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.FeatureToggles;
using Calamari.Kubernetes.Commands.Executors;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Apply Raw Yaml to Kubernetes Cluster")]
    public class KubernetesApplyRawYamlCommand : KubernetesDeploymentCommandBase
    {
        public const string Name = "kubernetes-apply-raw-yaml";

        private readonly ILog log;
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusChecker resourceStatusChecker;
        private readonly Kubectl kubectl;

        public KubernetesApplyRawYamlCommand(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IResourceStatusChecker resourceStatusChecker,
            Kubectl kubectl)
            : base(log, deploymentJournalWriter, variables, fileSystem, extractPackage,
            substituteInFiles, structuredConfigVariablesService, kubectl)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.resourceStatusChecker = resourceStatusChecker;
            this.kubectl = kubectl;
        }

        public override int Execute(string[] commandLineArguments)
        {
            if (!FeatureToggle.MultiGlobPathsForRawYamlFeatureToggle.IsEnabled(variables))
                throw new InvalidOperationException(
                    "Unable to execute the Kubernetes Apply Raw YAML Command because the appropriate feature has not been enabled.");

            return base.Execute(commandLineArguments);
        }

        protected override async Task ExecuteCommand(RunningDeployment runningDeployment)
        {
            var statusReportExecutor = new ResourceStatusReportExecutor(variables, log, fileSystem,
                resourceStatusChecker, kubectl, new ResourceStatusReportExecutor.Settings
                    { FindResourcesFromFiles = false, ReceiveResourcesFromResourcesAppliedEvent = true });

            var gatherAndApplyRawYamlExecutor =
                new GatherAndApplyRawYamlExecutor(log, fileSystem, kubectl);

            statusReportExecutor.StartReportingStatus(runningDeployment.CurrentDirectory);

            gatherAndApplyRawYamlExecutor.Execute(runningDeployment, statusReportExecutor.AddResources);

            await statusReportExecutor.WaitForStatusReportingToComplete();
        }
    }
}
#endif