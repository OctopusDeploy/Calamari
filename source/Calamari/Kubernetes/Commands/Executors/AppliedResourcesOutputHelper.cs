using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Commands.Executors
{
    public static class AppliedResourcesOutputHelper
    {
        public static void SetAppliedResourcesOutputVariable(
            ILog log, 
            RunningDeployment deployment, 
            IEnumerable<ResourceIdentifier> resources)
        {
            if (!OctopusFeatureToggles.ArgoRolloutsSupportFeatureToggle.IsEnabled(deployment.Variables))
            {
                return;
            }

            var resourceList = resources.Select(r => new
            {
                r.GroupVersionKind.Group,
                r.GroupVersionKind.Version,
                r.GroupVersionKind.Kind,
                r.Name,
                r.Namespace
            }).ToArray();

            var json = JsonConvert.SerializeObject(resourceList);

            log.SetOutputVariable("AppliedResources", json, deployment.Variables);
        }
    }
}
