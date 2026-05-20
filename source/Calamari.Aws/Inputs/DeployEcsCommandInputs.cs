using System;
using System.Collections.Generic;
using System.Linq;
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

}

public record InputsValidityResult(IEnumerable<string> MissingKeys)
{
    public bool IsValid => !MissingKeys.Any();
}
