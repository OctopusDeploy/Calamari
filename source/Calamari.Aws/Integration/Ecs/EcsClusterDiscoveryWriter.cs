using Amazon.ECS.Model;
using Calamari.Aws.Discovery;
using Calamari.Common.Features.Discovery;
using Octopus.Calamari.Contracts.TargetDiscovery;

namespace Calamari.Aws.Integration.Ecs;


public interface IEcsClusterDiscoveryWriter
{
    void WriteTargetCreationServiceMessage(
        string region,
        Cluster cluster,
        IAwsAuthenticationDetails authentication,
        TargetDiscoveryScope scope,
        TargetMatchResult matchResult);
}
    
public class EcsClusterDiscoveryWriter: IEcsClusterDiscoveryWriter
{
    public void WriteTargetCreationServiceMessage(string region,
                                                  Cluster cluster,
                                                  IAwsAuthenticationDetails authentication,
                                                  TargetDiscoveryScope scope,
                                                  TargetMatchResult matchResult)
    {
        throw new System.NotImplementedException();
    }
}