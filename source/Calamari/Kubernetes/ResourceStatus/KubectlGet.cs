using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IKubectlGet
    {
        ICommandOutput Resource(string kind, string name, string @namespace, IKubectl kubectl);
        ICommandOutput AllResources(string kind, string @namespace, IKubectl kubectl);
    }

    public class KubectlGet : IKubectlGet
    {
        public ICommandOutput Resource(string kind, string name, string @namespace, IKubectl kubectl)
        {
            return kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", kind, name, "-o=jsonpath=\"{@}\"", string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}" }).Output;
        }

        public ICommandOutput AllResources(string kind, string @namespace, IKubectl kubectl)
        {
            return kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", kind, "-o=jsonpath=\"{@}\"", string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}"
            }).Output;
        }
    }
}