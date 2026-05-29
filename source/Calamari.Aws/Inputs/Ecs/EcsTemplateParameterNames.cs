namespace Calamari.Aws.Inputs.Ecs;

// CloudFormation parameter names used by EcsDeployTemplate. Shared between the
// generator (which sets Defaults and supplies override values at deploy time)
// and the template (which looks them up by name when wiring CDK Refs).
public static class EcsTemplateParameterNames
{
    public const string ClusterName          = "ClusterName";
    public const string TaskDefinitionName   = "TaskDefinitionName";
    public const string TaskDefinitionCpu    = "TaskDefinitionCPU";
    public const string TaskDefinitionMemory = "TaskDefinitionMemory";
    public const string TaskRole             = "TaskRole";
    public const string TaskExecutionRole    = "TaskExecutionRole";
    public const string LogGroupName         = "LogGroupName";
}
