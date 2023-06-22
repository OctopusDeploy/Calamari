using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes
{
    public static class ResourceLoggingExtensionMethods
    {
        public static void LogResources<T>(this ILog log, IEnumerable<T> resourceToLog) where T : IResourceIdentity
        {
            foreach (var resourceIdentifier in resourceToLog)
            {
                log.Verbose($" - {resourceIdentifier.Kind}/{resourceIdentifier.Name} in namespace {resourceIdentifier.Namespace}");
            }
        }
    }
}