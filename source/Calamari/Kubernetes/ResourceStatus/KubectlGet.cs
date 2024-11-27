using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IKubectlGet
    {
        KubectlGetResult Resource(string group, string kind, string name, string @namespace, IKubectl kubectl);
        KubectlGetResult AllResources(string group, string kind, string @namespace, IKubectl kubectl);
    }

    public class KubectlGet : IKubectlGet
    {
        public KubectlGetResult Resource(string group, string kind, string name, string @namespace, IKubectl kubectl)
        {
            var output = kubectl.ExecuteCommandAndReturnOutput(new[]
                                {
                                    "get", $"{kind}.{group}", name, "-o=jsonpath=\"{@}\"", string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}"
                                })
                                .Output;

            return new KubectlGetResult(output.InfoLogs.Join(string.Empty),
                                        output.Messages.Select(msg => $"{msg.Level}: {msg.Text}").ToList());
        }

        public KubectlGetResult AllResources(string group, string kind, string @namespace, IKubectl kubectl)
        {
            var output = kubectl.ExecuteCommandAndReturnOutput(new[]
                                {
                                    "get", $"{kind}.{group}", "-o=jsonpath=\"{@}\"", string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}"
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