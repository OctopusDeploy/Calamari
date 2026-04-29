using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.Ecs;
using Calamari.Aws.Integration.Ecs.Update;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;

namespace Calamari.Aws.Commands;

[Command("update-aws-ecs-service", Description = "Updates an ECS service to a new task definition revision")]
public class UpdateEcsServiceCommand : Command
{
    readonly ILog log;
    readonly IVariables variables;

    public UpdateEcsServiceCommand(ILog log, IVariables variables)
    {
        this.log = log;
        this.variables = variables;
    }

    public override int Execute(string[] commandLineArguments)
    {
        Options.Parse(commandLineArguments);

        var inputs = EcsUpdateServiceInputs.Parse(variables);
        var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
        var mergedTags = EcsUpdateTags.Merge(variables, inputs.UserTags);

        using var ecsClient = EcsClientFactory.Create(environment);

        new ConventionProcessor(new RunningDeployment(variables),
        [
            new LogAwsUserInfoConvention(environment),
            new UpdateEcsServiceConvention(ecsClient, inputs, mergedTags, log),
            new SetEcsUpdateServiceOutputVariablesConvention(environment, inputs.ClusterName, inputs.ServiceName, log)
        ], log).RunConventions();

        return 0;
    }
}
