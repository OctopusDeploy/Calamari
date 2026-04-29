using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions;

public class SetEcsUpdateServiceOutputVariablesConvention : IInstallConvention
{
    readonly AwsEnvironmentGeneration environment;
    readonly string clusterName;
    readonly string serviceName;
    readonly ILog log;

    public SetEcsUpdateServiceOutputVariablesConvention(
        AwsEnvironmentGeneration environment, string clusterName, string serviceName, ILog log)
    {
        this.environment = environment;
        this.clusterName = clusterName;
        this.serviceName = serviceName;
        this.log = log;
    }

    public void Install(RunningDeployment deployment)
    {
        Set(deployment.Variables, "ClusterName", clusterName);
        Set(deployment.Variables, "ServiceName", serviceName);
        Set(deployment.Variables, "Region", environment.AwsRegion.SystemName);

        var family = deployment.Variables.Get(UpdateEcsServiceConvention.OutputFamilyVar);
        var revision = deployment.Variables.Get(UpdateEcsServiceConvention.OutputRevisionVar);
        if (!string.IsNullOrEmpty(family))
        {
            Set(deployment.Variables, "TaskDefinitionFamily", family);
        }
        if (!string.IsNullOrEmpty(revision))
        {
            Set(deployment.Variables, "TaskDefinitionRevision", revision);
        }
    }

    void Set(IVariables variables, string name, string value)
    {
        log.Info($"Saving variable \"Octopus.Action[{variables["Octopus.Action.Name"]}].Output.{name}\"");
        log.SetOutputVariable(name, value, variables);
    }
}
