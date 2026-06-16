
using Amazon.ECS;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;

namespace Calamari.Aws.Integration.Ecs;

// TODO: Replace Static Helper with concrete class/interface to enable better testing
public static class EcsClientFactoryHelper
{
    public static IAmazonECS Create(AwsEnvironmentGeneration environment) =>
        new AmazonECSClient(environment.AwsCredentials, environment.AsClientConfig<AmazonECSConfig>());
}
