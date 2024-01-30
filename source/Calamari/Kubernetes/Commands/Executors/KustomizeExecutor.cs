#if !NET40
using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.Commands.Executors
{
    class KustomizeExecutor : BaseKubernetesApplyExecutor
    {
        public KustomizeExecutor(ILog log, Kubectl kubectl) : base(log, kubectl)
        {
        }

        protected override IEnumerable<ResourceIdentifier> ApplyAndGetResourceIdentifiers(RunningDeployment deployment)
        {
            throw new NotImplementedException();
        }
    }
}
#endif