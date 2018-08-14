using Octostache;

namespace Calamari.Shared.HealthChecks
{
    public interface IDoesDeploymentTargetTypeHealthChecks
    {
        bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName);

        int ExecuteHealthCheck(VariableDictionary variables);
    }
}