using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IKubectlGet
    {
        KubectlGetResult Resource(IResourceIdentity resourceIdentity, IKubectl kubectl);
        KubectlGetResult AllResources(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl);
    }

    public class KubectlGet : IKubectlGet
    {
        public KubectlGetResult Resource(IResourceIdentity resourceIdentity, IKubectl kubectl)
        {
            var output = kubectl.ExecuteCommandAndReturnOutput(new[]
                                {
                                    "get", 
                                    $"{resourceIdentity.GroupVersionKind.Kind}.{resourceIdentity.GroupVersionKind.Version}.{resourceIdentity.GroupVersionKind.Group}", 
                                    resourceIdentity.Name, 
                                    "-o=jsonpath=\"{@}\"", 
                                    string.IsNullOrEmpty(resourceIdentity.Namespace) ? "" : $"-n {resourceIdentity.Namespace}"
                                })
                                .Output;

            return new KubectlGetResult(output.InfoLogs.Join(string.Empty),
                                        output.Messages.Select(msg => $"{msg.Level}: {msg.Text}").ToList());
        }

        public KubectlGetResult AllResources(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl)
        {
            var output = kubectl.ExecuteCommandAndReturnOutput(new[]
                                {
                                    "get", 
                                    $"{groupVersionKind.Kind}.{groupVersionKind.Version}.{groupVersionKind.Group}", 
                                    "-o=jsonpath=\"{@}\"", 
                                    string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}"
                                })
                                .Output;

            return new KubectlGetResult(output.InfoLogs.Join(string.Empty),
                                        output.Messages.Select(msg => $"{msg.Level}: {msg.Text}").ToList());
        }
    }

    public class KubectlGetResult
    {
        public KubectlGetResult(string resourceJson, IList<string> rawOutput)
        {
            ResourceJson = resourceJson;
            RawOutput = rawOutput;
        }

        public string ResourceJson { get; }

        public IList<string> RawOutput { get; }
    }
}