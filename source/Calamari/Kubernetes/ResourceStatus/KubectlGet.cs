using System;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IKubectlGet
    {
        string Resource(string kind, string name, string @namespace, IKubectl kubectl);
        string AllResources(string kind, string @namespace, IKubectl kubectl);
    }

    public class KubectlGet : IKubectlGet
    {
        public string Resource(string kind, string name, string @namespace, IKubectl kubectl)
        {
            return kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", kind, name, "-o json", string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}"
            }).Output.InfoLogs.Join(Environment.NewLine);
        }

        public string AllResources(string kind, string @namespace, IKubectl kubectl)
        {
            return kubectl.ExecuteCommandAndReturnOutput(new[]
            {
                "get", kind, "-o json", string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}"
            }).Output.InfoLogs.Join(Environment.NewLine);
        }
    }
}