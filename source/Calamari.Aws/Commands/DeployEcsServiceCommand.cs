using System;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.Ecs;
using Calamari.CloudAccounts;
using Calamari.CloudAccounts.Aws;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;

namespace Calamari.Aws.Commands;

[Command(CommandName, Description = "Deploys a service to an Amazon ECS cluster")]
public class DeployEcsServiceCommand(ILog log, IVariables variables, IEcsStackNameGenerator stackNameGenerator, IEcsImageNameResolver ecsImageNameResolver) : Command
{
    const string CommandName = "deploy-aws-ecs-service";

    public override int Execute(string[] commandLineArguments)
    {
        var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
        var inputs = new DeployEcsCommandInputs(variables, stackNameGenerator, ecsImageNameResolver, log);
        var inputValidity = inputs.Validate();
        if (!inputValidity.IsValid)
        {
            throw new CommandException(inputValidity.MissingKeyList);
        }


        new ConventionProcessor(new RunningDeployment(variables),
                                [
                                    new LogAwsUserInfoConvention(environment),
                                    new DeployEcsServiceConvention(inputs, environment, log, variables),
                                    new SetEcsOutputVariablesConvention(environment,
                                                                        inputs.CfStackName,
                                                                        inputs.ClusterName,
                                                                        inputs.ServiceTaskName, 
                                                                        log)
                                ],
                                log).RunConventions();

        return 0;
    }
}