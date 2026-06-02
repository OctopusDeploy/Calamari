using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Inputs.Ecs;

public class DeployEcsCommandInputs
{
    readonly IVariables variables;
    readonly IEcsStackNameGenerator stackNameGenerator;
    readonly ILog log;
    readonly HashSet<string> requiredVariableKeys = [];

    public DeployEcsCommandInputs(IVariables variables, IEcsStackNameGenerator stackNameGenerator, ILog log)
    {
        this.variables = variables;
        this.stackNameGenerator = stackNameGenerator;
        this.log = log;

        // strings
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.ClusterName);
        requiredVariableKeys.Add(DeploymentEnvironment.Id);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.Cpu);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.Memory);

        // primitives
        // TODO: Type checking
        // TODO: Defaults?
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.DesiredCount);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags);

        // collections
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.SubnetIds);

        // Objects
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.WaitOption);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.Containers);
    }

    public string ClusterName => variables.Get(AwsSpecialVariables.Ecs.ClusterName);

    public string ServiceTaskName => variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName);

    public string CfStackName
    {
        get
        {
            var stackNameValue = variables.Get(AwsSpecialVariables.Ecs.Deploy.StackName);
            if (string.IsNullOrEmpty(stackNameValue))
            {
                stackNameValue = stackNameGenerator.Generate(ClusterName, ServiceName, Environment, Tenant);
                log.Verbose($"No stack name supplied; generated \"{stackNameValue}\".");
            }

            return stackNameValue;
        }
    }

    public StackArn CfStackArn => new StackArn(CfStackName);

    public string Environment => variables.GetMandatoryVariable(DeploymentEnvironment.Id);

    public string Tenant => variables.Get(DeploymentVariables.Tenant.Id, "");

    public string Cpu => variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.Cpu);

    public string Memory => variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.Memory);

    public double DesiredCount => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.DesiredCount));
    public double MinimumHealthyPercentage => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent));
    public double MaximumHealthyPercentage => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent));

    public string AutoAssignPublicIp => variables.GetFlag(AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp) ? "ENABLED" : "DISABLED";

    public bool EnableEcsManagedTags => variables.GetFlag(AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags);

    public string TaskRole => variables.Get(AwsSpecialVariables.Ecs.Deploy.TaskRole, "");
    public string TaskExecutionRole => variables.Get(AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole, "");

    public string CpuArchitecture
    {
        get
        {
            var cpuArchValue = variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform);
            return cpuArchValue.ToUpper() switch
            {
                "ARM64" => "ARM64",
                _ => "X86_64" // default
            };
        }
    }

    public string[] NetworkSecurityGroupIds => variables.GetValueDeserialisedAs<string[]>(AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds);
    public string[] SubnetIDs => variables.GetValueDeserialisedAs<string[]>(AwsSpecialVariables.Ecs.Deploy.SubnetIds);

    public WaitOption WaitOption => variables.GetValueDeserialisedAs<WaitOption>(AwsSpecialVariables.Ecs.WaitOption);

    public ContainerSpec[] Containers => variables.GetValueDeserialisedAs<ContainerSpec[]>(AwsSpecialVariables.Ecs.Deploy.Containers);

    public KeyValuePair<string, string>[] Tags => variables.GetValueDeserialisedAs<KeyValuePair<string, string>[]>(AwsSpecialVariables.Ecs.Tags);

    public LoadBalancerMapping[] LoadBalancerMappings => variables.GetValueDeserialisedAs<LoadBalancerMapping[]>(AwsSpecialVariables.Ecs.Deploy.LoadBalancerMappings);

    public Volume[] Volumes => variables.GetValueDeserialisedAs<Volume[]>(AwsSpecialVariables.Ecs.Deploy.Volumes);

    public bool RequiresLogGroup => Containers.Any(c => c.ContainerLogging.Type == ContainerLoggingType.Auto);

    public bool ShouldWaitForDeploymentCompletion => WaitOption.Type is WaitType.WaitUntilCompleted or WaitType.WaitWithTimeout;

    public InputsValidityResult Validate()
    {
        var variableNames = variables.GetNames();
        var missingKeys = requiredVariableKeys.Except(variableNames);

        // TODO: Validation of input values

        return new InputsValidityResult(missingKeys);
    }

#pragma warning disable CS0618 // Type or member is obsolete temporary SPF deprecation
    public string ServiceName => $"Service{variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName).CamelCase()}";

    public string TaskName => $"TaskDefinition{variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName).CamelCase()}";

    public string FallbackTaskExecutionRoleName => $"TaskExecutionRole{variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName).CamelCase()}";

    public string LogGroupName => $"AwsLogGroup{variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName).CamelCase()}";

    public string DefaultLogGroupPath => $"/ecs/{ServiceTaskName}";
#pragma warning restore CS0618 // Type or member is obsolete
}

public record InputsValidityResult(IEnumerable<string> MissingKeys)
{
    public bool IsValid => !MissingKeys.Any();
    
    public string MissingKeyList {
        get
        {
            var title = $"The following Property keys were missing{Environment.NewLine}";
            var body = string.Join(Environment.NewLine, MissingKeys.Select(p => $"- {p}"));
            return title + body;
        }
    }
}

public static class EcsInputDefaults
{
    public const double DesiredCount = 1;
    public const double MinimumHealthPercent = 100;
    public const double MaximumHealthPercent = 200;
}