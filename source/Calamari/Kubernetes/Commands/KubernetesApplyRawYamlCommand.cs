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

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Apply Raw Yaml to Kubernetes Cluster")]
    public class KubernetesApplyRawYamlCommand : KubernetesDeploymentCommandBase
    {
        public const string Name = "kubernetes-apply-raw-yaml";

        private readonly ILog log;
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IResourceStatusReportExecutor statusReporter;
        private readonly IGatherAndApplyRawYamlExecutor gatherAndApplyRawYamlExecutor;
        private readonly Kubectl kubectl;

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
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.statusReporter = statusReporter;
            this.gatherAndApplyRawYamlExecutor = gatherAndApplyRawYamlExecutor;
            this.kubectl = kubectl;
        }

        public override int Execute(string[] commandLineArguments)
        {
            if (!FeatureToggle.MultiGlobPathsForRawYamlFeatureToggle.IsEnabled(variables))
                throw new InvalidOperationException(
                    "Unable to execute the Kubernetes Apply Raw YAML Command because the appropriate feature has not been enabled.");

            return base.Execute(commandLineArguments);
        }

        protected override async Task<bool> ExecuteCommand(RunningDeployment runningDeployment)
        {
            if (!variables.GetFlag(SpecialVariables.ResourceStatusCheck))
            {
                return await gatherAndApplyRawYamlExecutor.Execute(runningDeployment);
            }
            
            var statusCheck = statusReporter.Start();

            return await gatherAndApplyRawYamlExecutor.Execute(runningDeployment, statusCheck.AddResources) &&
                await statusCheck.WaitForCompletionOrTimeout();
        }
    }
}
#endif