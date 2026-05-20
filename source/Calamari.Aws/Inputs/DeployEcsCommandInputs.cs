using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK.AWS.ECS;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Inputs;

public class DeployEcsCommandInputs
{
    readonly IVariables variables;
    readonly IEcsStackNameGenerator stackNameGenerator;
    readonly ILog log;
    readonly List<string> requiredVariableKeys = [];

    public DeployEcsCommandInputs(IVariables variables, IEcsStackNameGenerator stackNameGenerator, ILog log)
    {
        this.variables = variables;
        this.stackNameGenerator = stackNameGenerator;
        this.log = log;

        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.ClusterName);
        requiredVariableKeys.Add(DeploymentEnvironment.Id);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName);
        
        // TODO: Type checking
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.Cpu);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.Memory);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.DesiredCount);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent);
        requiredVariableKeys.Add(AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent);


    }
    
    public InputsValidityResult Validate()
    {
        var variableNames = variables.GetNames();
        var missingKeys = requiredVariableKeys.Except(variableNames);
        
        // TODO: Validation of input values
        
        
        return new InputsValidityResult(missingKeys);
    }

    public string ClusterName => variables.Get(AwsSpecialVariables.Ecs.ClusterName);

#pragma warning disable CS0618 // Type or member is obsolete temporary SPF deprecation
    public string ServiceName => $"Service{variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName).CamelCase()}";
    
    public string TaskName  => $"TaskDefinition{variables.Get(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName).CamelCase()}";
#pragma warning restore CS0618 // Type or member is obsolete

    
    public string CfStackName {
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
    
    public StackArn CfStackArn => new(CfStackName); //Look at why we even need this? 

    public string Environment => variables.GetMandatoryVariable(DeploymentEnvironment.Id);
    
    public string Tenant => variables.Get(DeploymentVariables.Tenant.Id, "");

    public double Cpu => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.Cpu));

    public double Memory => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.Memory));

    public double DesiredCount => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.DesiredCount));
    public double MinimumHealthyPercentage => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent));
    public double MaximumHealthyPercentage => double.Parse(variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent));
    
    public CpuArchitecture CpuArchitecture
    {
        get
        {
            var cpuArchValue = variables.GetMandatoryVariable(AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform);
            return cpuArchValue.ToUpper() switch
                   {
                       "ARM64" => CpuArchitecture.ARM64,
                       _       => CpuArchitecture.X86_64  // default
                   };
        }
    }

}

public record InputsValidityResult(IEnumerable<string> MissingKeys)
{
    public bool IsValid => !MissingKeys.Any();
}
