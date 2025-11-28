using System;
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
            var commandResult = kubectl.ExecuteCommandAndReturnOutput("get",
                                $"{resourceIdentity.GroupVersionKind.Kind}.{resourceIdentity.GroupVersionKind.Version}.{resourceIdentity.GroupVersionKind.Group}",
                                resourceIdentity.Name,
                                "-o=jsonpath=\"{@}\"",
                                string.IsNullOrEmpty(resourceIdentity.Namespace) ? "" : $"-n {resourceIdentity.Namespace}");

            return ProcessResult(commandResult);
        }

        public KubectlGetResult AllResources(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl)
        {
            var commandResult = kubectl.ExecuteCommandAndReturnOutput("get",
                                $"{groupVersionKind.Kind}.{groupVersionKind.Version}.{groupVersionKind.Group}",
                                "-o=jsonpath=\"{@}\"",
                                string.IsNullOrEmpty(@namespace) ? "" : $"-n {@namespace}");

            return ProcessResult(commandResult);
        }

        static KubectlGetResult ProcessResult(CommandResultWithOutput commandResult)
        {
            return new KubectlGetResult(
                commandResult.Output.InfoLogs.Join(string.Empty),
                commandResult.Output.Messages.Select(msg => $"{msg.Level}: {msg.Text}").ToList(),
                commandResult.Result.ExitCode);
        }
    }

    public class KubectlGetResult
    {
        public KubectlGetResult(string resourceJson, IList<string> rawOutput, int exitCode)
        {
            ResourceJson = resourceJson;
            RawOutput = rawOutput;
            ExitCode = exitCode;
        }

        public string ResourceJson { get; }

        public IList<string> RawOutput { get; }

        public int ExitCode { get; }
    }
}
