using System;
using Calamari.Common.Features.Discovery;

namespace Calamari.Kubernetes.Commands.Discovery
{
    public class Cluster
    {
        public Cluster(string name, string resourceGroupName, string accountId, TargetTags tags)
        {
            Name = name;
            ResourceGroupName = resourceGroupName;
            AccountId = accountId;
            Tags = tags;
        }

        public string Name { get; }
        
        public string ResourceGroupName { get; }
        
        public string AccountId { get; }
        
        public TargetTags Tags { get; }
        
        //TODO:
    }
}