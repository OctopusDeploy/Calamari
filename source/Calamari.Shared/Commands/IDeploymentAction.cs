namespace Calamari.Shared.Commands
{
    public interface IDeploymentAction
    {
        void Build(IDeploymentStrategyBuilder deploymentStrategyBuilder);
    }
}