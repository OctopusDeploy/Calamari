using Calamari.Common.Commands;

namespace Calamari.Deployment.Features
{
    public interface IFeature
    {
        string Name { get; }

        string DeploymentStage { get; }

        void Execute(RunningDeployment deployment);
    }
}