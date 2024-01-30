#if !NET40
using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Apply Kubernetes manifests with Kustomize")]
    public class KubernetesKustomizeCommand : KubernetesDeploymentCommandBase
    {
        public const string Name = "kubernetes-kustomize";

        public KubernetesKustomizeCommand(
            ILog log, 
            IDeploymentJournalWriter deploymentJournalWriter, 
            IVariables variables, 
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            Kubectl kubectl) : base(log, deploymentJournalWriter, variables, fileSystem,
                                    extractPackage,
                                    substituteInFiles,
                                    structuredConfigVariablesService,
                                    kubectl)
        {
        }
    }
}
#endif