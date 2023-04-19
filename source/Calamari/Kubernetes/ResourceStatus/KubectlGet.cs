using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IKubectlGet
    {
        string Resource(string kind, string name, string @namespace, Kubectl kubectl);
        string AllResources(string kind, string @namespace, Kubectl kubectl);
    }

    public class KubectlGet : IKubectlGet
    {
        public string Resource(string kind, string name, string @namespace, Kubectl kubectl)
        {
            return kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", kind, name, "-o json", $"-n {@namespace}"
            }).Output.InfoLogs.Join(string.Empty);
        }

        public string AllResources(string kind, string @namespace, Kubectl kubectl)
        {
            return kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", kind, "-o json", $"-n {@namespace}"
            }).Output.InfoLogs.Join(string.Empty);
        }
    }
}