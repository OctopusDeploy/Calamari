using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus;

public static class ServiceMessages
{
    public static void Send(IEnumerable<Resource> resources, IVariables variables, ILog log)
    {
        var data = GenerateServiceMessageData(resources);

        var parameters = new Dictionary<string, string>
        {
            {"data", data},
            {"deploymentId", variables.Get("Octopus.Deployment.Id")},
            {"actionId", variables.Get("Octopus.Action.Id")}
        };

        var message = new ServiceMessage("kubernetes-deployment-status-update", parameters);
        log.WriteServiceMessage(message);
    }

    public static string GenerateServiceMessageData(IEnumerable<Resource> resources)
    {
        var result = resources
            .GroupBy(resource => resource.Kind)
            .ToDictionary(
                group => group.Key,
                group => group.Select(CreateEntry));
        return JsonConvert.SerializeObject(result);
    }

    private static MessageEntry CreateEntry(Resource resource)
    {
        var status= resource.Status;
        return new MessageEntry
        {
            ResourceStatus = status,
            Message = "",
            Data = resource.Data
        };
    }

    public class MessageEntry
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ResourceStatus ResourceStatus { get; set; }
        public string Message { get; set; }
        public JObject Data { get; set; }
    }
}