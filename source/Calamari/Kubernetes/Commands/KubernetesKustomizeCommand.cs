using System;
using System.Threading;
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
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Apply Kubernetes manifests with Kustomize")]
    public class KubernetesKustomizeCommand : KubernetesDeploymentCommandBase
    {
        public const string Name = "kubernetes-kustomize";
        readonly IVariables variables;
        readonly IKubernetesApplyExecutor kubernetesApplyExecutor;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly ILog log;

        public KubernetesKustomizeCommand(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IKustomizeKubernetesApplyExecutor kubernetesApplyExecutor,
            IResourceStatusReportExecutor statusReporter,
            Kubectl kubectl) : base(log,
                                    deploymentJournalWriter,
                                    variables,
                                    fileSystem,
                                    extractPackage,
                                    substituteInFiles,
                                    structuredConfigVariablesService,
                                    kubectl)
        {
            this.log = log;
            this.variables = variables;
            this.kubernetesApplyExecutor = kubernetesApplyExecutor;
            this.statusReporter = statusReporter;
        }

        protected override async Task<bool> ExecuteCommand(RunningDeployment runningDeployment)
        {
            if (!variables.GetFlag(SpecialVariables.ResourceStatusCheck))
            {
                return await kubernetesApplyExecutor.Execute(runningDeployment);
            }

            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var statusCheck = statusReporter.Start(timeoutSeconds, waitForJobs);

            return await kubernetesApplyExecutor.Execute(runningDeployment, AddNewResources) 
                   && await statusCheck.WaitForCompletionOrTimeout(CancellationToken.None);

            Task AddNewResources(ResourceIdentifier[] newResources)
            {
                log.Info($"Performing resource status check for {newResources.Length} resources");
                return statusCheck.AddResources(newResources);
            }
        }
    }
}