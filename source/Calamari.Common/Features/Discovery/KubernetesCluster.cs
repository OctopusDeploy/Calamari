using System;

namespace Calamari.Common.Features.Discovery
{
    public class KubernetesCluster
    {
        public KubernetesCluster(string name, string resourceGroupName, string accountId, TargetTags tags)
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
    }
}