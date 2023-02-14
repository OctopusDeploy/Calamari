using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;
using Microsoft.Azure.Management.Sql.Fluent.Models;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes;

public class KubernetesResource
{
    public string Kind { get; set; }
    public string Name { get; set; }
    public string Uid { get; set; }
}

public interface IKubernetesResourceStatusChecker
{
    IEnumerable<JObject> GetHierarchyStatuses(KubernetesResource resource, ICommandLineRunner commandLineRunner);
}

public class KubernetesResourceStatusChecker : IKubernetesResourceStatusChecker
{
    private readonly IKubectlCommand kubernetesCluster;
    
    public KubernetesResourceStatusChecker(IKubectlCommand kubernetesCluster)
    {
        this.kubernetesCluster = kubernetesCluster;
    }

    public IEnumerable<JObject> GetHierarchyStatuses(KubernetesResource resource, ICommandLineRunner commandLineRunner)
    {
        var resources = new List<KubernetesResource> { resource };
        var statuses = new List<JObject>();
        
        var current = 0;
        while (current < resources.Count)
        {
            var status = GetStatus(resources[current], commandLineRunner);
            statuses.Add(status);
            // Add the UID field before trying to fetch children
            resources[current] = GetResource(status);

            var children = GetChildrenStatuses(resources[current].Uid, GetChildType(resources[current]), commandLineRunner);
            resources.AddRange(children.Select(GetResource));

            ++current;
        }

        return statuses;
    }

    JObject GetStatus(KubernetesResource resource, ICommandLineRunner commandLineRunner)
    {
        var result = kubernetesCluster.Get(resource.Kind, resource.Name, commandLineRunner);
        return JObject.Parse(result);
    }
    
    IEnumerable<JObject> GetChildrenStatuses(string ownerUid, string kind, ICommandLineRunner commandLineRunner)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return Enumerable.Empty<JObject>();
        }
        var result = kubernetesCluster.GetAll(kind, commandLineRunner);
        var data = JObject.Parse(result);
        var items = data.SelectTokens($"$.items[?(@.metadata.ownerReferences[0].uid == '{ownerUid}')]");
        return items.Cast<JObject>();
    }

    KubernetesResource GetResource(JObject status)
    {
        return new KubernetesResource
        {
            Kind = status.SelectToken("$.kind").Value<string>(),
            Name = status.SelectToken("$.metadata.name").Value<string>(),
            Uid = status.SelectToken("$.metadata.uid").Value<string>()
        };
    }
    
    string GetChildType(KubernetesResource resource)
    {
        switch (resource.Kind)
        {
            case "Deployment":
                return "ReplicaSet";
            case "ReplicaSet":
                return "Pod";
        }

        return "";
    }
}