namespace Calamari.HealthChecks
{
    public interface IDoesDeploymentTargetTypeHealthChecks
    {
        bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName);

        int ExecuteHealthCheck();
    }
}