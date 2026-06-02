using Amazon.ECS;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;

namespace Calamari.Aws.Integration.Ecs;

public static class EcsClientFactory
{
    public static IAmazonECS Create(AwsEnvironmentGeneration environment) =>
        new AmazonECSClient(environment.AwsCredentials, environment.AsClientConfig<AmazonECSConfig>());
}
