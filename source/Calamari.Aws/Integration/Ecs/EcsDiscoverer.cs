using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Runtime;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Aws.Integration.Ecs;

public interface IEcsDiscoverer
{
    Task<IReadOnlyList<Cluster>> DiscoverClustersInRegion(AWSCredentials credentials, string region);
}

public class EcsDiscoverer(IEcsClientFactory clientFactory, ILog log) : IEcsDiscoverer
{
    // DescribeClusters accepts at most 100 cluster identifiers per request.
    const int DescribeClustersBatchSize = 100;

    public async Task<IReadOnlyList<Cluster>> DiscoverClustersInRegion(AWSCredentials credentials, string region)
    {
        using var client = clientFactory.Create(credentials, region);
        log.Verbose($"Listing ECS clusters in region {region}.");

        var clusterArns = new List<string>();
        string nextToken = null;
        do
        {
            var response = await client.ListClustersAsync(new ListClustersRequest { NextToken = nextToken });
            clusterArns.AddRange(response.ClusterArns ?? Enumerable.Empty<string>());
            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));

        var clusters = new List<Cluster>();
        foreach (var batch in clusterArns.Chunk(DescribeClustersBatchSize))
        {
            var response = await client.DescribeClustersAsync(new DescribeClustersRequest
            {
                Clusters = batch.ToList(),
                // Tags aren't returned by default and are required to match the discovery scope.
                Include = [ClusterField.TAGS]
            });

            clusters.AddRange((response.Clusters ?? Enumerable.Empty<Cluster>())
                .Where(cluster => cluster.Status != "INACTIVE"));
        }

        log.Verbose($"Found {clusters.Count} active ECS cluster(s) in region {region}.");
        return clusters;
    }
}