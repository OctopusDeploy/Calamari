using Amazon;
using Amazon.ECS;
using Amazon.Runtime;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;

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


// TODO: Replace Static Helper with concrete class/interface to enable better testing
public static class EcsClientFactoryHelper
{
    public static IAmazonECS Create(AwsEnvironmentGeneration environment) =>
        new AmazonECSClient(environment.AwsCredentials, environment.AsClientConfig<AmazonECSConfig>());
}
