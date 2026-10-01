using Amazon;
using Amazon.ECS;
using Amazon.Runtime;

namespace Calamari.Aws.Integration.Ecs;

public interface IEcsClientFactory
{
    IAmazonECS Create(AWSCredentials credentials, string region);
}

public class EcsClientFactory : IEcsClientFactory
{
    public IAmazonECS Create(AWSCredentials credentials, string region)
    {
        return new AmazonECSClient(credentials, RegionEndpoint.GetBySystemName(region));
    }
}