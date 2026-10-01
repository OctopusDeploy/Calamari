using System;
using System.Linq;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Deployment.Conventions;

public class EcsClusterHealthCheckConvention (string clusterName, Func<IAmazonECS> clientFactory, ILog log): IInstallConvention
{
    public void Install(RunningDeployment deployment)
    {
        InstallAsync().GetAwaiter().GetResult();
    }

    async Task InstallAsync()
    {
        Guard.NotNullOrWhiteSpace(clusterName, "ClusterName should not be null or empty");

        using var ecsClient = clientFactory();
        
        var listClustersRequest = new DescribeClustersRequest()
        {
            Clusters = [clusterName]
        };
        var response = await ecsClient.DescribeClustersAsync(listClustersRequest);
        log.Verbose($"Found {response.Clusters?.Count ?? 0} cluster(s)");
        
        // Future: Do we want to handle writing out Failures list from Response?
        
        var exists = response.Clusters?.Any(c => c.Status != "INACTIVE") ?? false;
        if (!exists)
        {
            throw new ClusterNotFoundException($"Unable to find Cluster: {clusterName}, or Cluster is Inactive");
        }
    }
}