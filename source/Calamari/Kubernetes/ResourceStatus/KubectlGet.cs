using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            // these payloads are a whole namespace of one kind, so we avoid copying them
            var messages = commandResult.Output.Messages;

            var resourceJson = new StringBuilder();
            foreach (var message in messages)
            {
                if (message.Level == Level.Info)
                    resourceJson.Append(message.Text);
            }

            return new KubectlGetResult(resourceJson.ToString(), messages, commandResult.Result.ExitCode);
        }
    }

    public class KubectlGetResult
    {
        readonly Message[] messages;
        readonly IList<string> formattedOutput;

        public KubectlGetResult(string resourceJson, IList<string> rawOutput, int exitCode)
        {
            ResourceJson = resourceJson;
            formattedOutput = rawOutput;
            ExitCode = exitCode;
        }

        public KubectlGetResult(string resourceJson, Message[] messages, int exitCode)
        {
            ResourceJson = resourceJson;
            this.messages = messages;
            ExitCode = exitCode;
        }

        public string ResourceJson { get; }

        public int ExitCode { get; }

        public bool HasOutput => messages?.Length > 0 || formattedOutput?.Count > 0;

        public IEnumerable<string> RawOutput
            => formattedOutput ?? messages?.Select(msg => $"{msg.Level}: {msg.Text}") ?? Enumerable.Empty<string>();
    }
}
