using System;

namespace Calamari.Common.Features.Discovery
{
    public class KubernetesCluster
    {
        public static KubernetesCluster CreateForEks(string name,
            string clusterName,
            string endpoint,
            string? accountId,
            AwsAssumeRole? assumeRole,
            string? workerPool,
            TargetTags tags)
        {
            return new KubernetesCluster(name,
                clusterName,
                null,
                endpoint,
                accountId,
                assumeRole,
                workerPool,
                tags,
                accountId == null);
        }

        public static KubernetesCluster CreateForAks(string name,
            string clusterName,
            string resourceGroupName,
            string accountId,
            TargetTags tags)
        {
            return new KubernetesCluster(name,
                clusterName,
                resourceGroupName,
                null,
                accountId,
                null,
                null,    
                tags);
        }

        KubernetesCluster(
            string name, 
            string clusterName, 
            string? resourceGroupName, 
            string? endpoint, 
            string? accountId, 
            AwsAssumeRole? awsAssumeRole, 
            string? workerPool,
            TargetTags tags,
            bool awsUseWorkerCredentials = false)
        {
            Name = name;
            ClusterName = clusterName;
            ResourceGroupName = resourceGroupName;
            Endpoint = endpoint;
            AccountId = accountId;
            AwsAssumeRole = awsAssumeRole;
            WorkerPool = workerPool;
            Tags = tags;
            AwsUseWorkerCredentials = awsUseWorkerCredentials;
        }

        public string? WorkerPool { get; }

        public string Name { get; }
        
        public string ClusterName { get; }
        
        public string? ResourceGroupName { get; }
        
        public string? Endpoint { get; }
        
        public string? AccountId { get; }
        
        public bool AwsUseWorkerCredentials { get; }
        
        public AwsAssumeRole? AwsAssumeRole { get; }
        
        public TargetTags Tags { get; }
    }

    public class AwsAssumeRole
    {
        public AwsAssumeRole(
            string arn, 
            string? session = null, 
            int? sessionDuration = null, 
            string? externalId = null)
        {
            Arn = arn;
            Session = session;
            SessionDuration = sessionDuration;
            ExternalId = externalId;
        }

        public string? ExternalId { get; set; }

        public int? SessionDuration { get; set; }

        public string? Session { get; set; }

        public string Arn { get; set; }
    }
}