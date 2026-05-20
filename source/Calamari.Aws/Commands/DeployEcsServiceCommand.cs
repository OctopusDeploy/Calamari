using System;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Inputs;
using Calamari.Aws.Integration.Ecs;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;


namespace Calamari.Aws.Commands;

[Command(CommandName, Description = "Deploys a service to an Amazon ECS cluster")]
public class DeployEcsServiceCommand(ILog log, IVariables variables, IEcsStackNameGenerator stackNameGenerator) : Command
{
    const string CommandName = "deploy-aws-ecs-service";

    public override int Execute(string[] commandLineArguments)
    {

        var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
        var inputs = new DeployEcsCommandInputs(variables, stackNameGenerator, log);
        var inputValidity = inputs.Validate();
        if (!inputValidity.IsValid)
        {
            // TODO: Better implementation
            throw new CommandException($"Invalid inputs provided to {CommandName}");
        }

        // var cloudFormationDeploymentConvention = new DeployEcsCloudFormationTemplateConventionFactory(inputs, environment, log).GetDeployConvention();
        var cloudFormationDeploymentConvention = new DeployEcsCloudFormationTemplateConventionFactory(inputs, log).GetDeployConvention();
        
        new ConventionProcessor(new RunningDeployment(variables),
                                [
                                    new LogAwsUserInfoConvention(environment),
                                    cloudFormationDeploymentConvention,
                                    new SetEcsOutputVariablesConvention(environment,
                                                                        inputs.CfStackName,
                                                                        inputs.ClusterName,
                                                                        inputs.ServiceName, // TODO: Check with Sathvik about implementation
                                                                        log)
                                ],
                                log).RunConventions();

        return 0;

        
    }

     // EcsCommandInputs ReadAndValidateInputs()
     // {
         // var clusterName = variables.Get(AwsSpecialVariables.Ecs.ClusterName);
         // Guard.NotNullOrWhiteSpace(clusterName, "Cluster name is required");
//     
//         var serviceName = variables.Get(AwsSpecialVariables.Ecs.ServiceName);
//         Guard.NotNullOrWhiteSpace(serviceName, "Service name is required");
//     

    //
    //     var serviceName = variables.Get(AwsSpecialVariables.Ecs.ServiceName);
    //     Guard.NotNullOrWhiteSpace(serviceName, "Service name is required");
    //
    //     var stackName = variables.Get(AwsSpecialVariables.CloudFormation.StackName);
    //     if (string.IsNullOrWhiteSpace(stackName))
    //     {
    //         stackName = stackNameGenerator.Generate(variables, clusterName, serviceName);
    //         log.Verbose($"No stack name supplied; generated \"{stackName}\".");
    //     }
    //
    //     var userTags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]") ?? [];
    //     var tags = EcsDefaultTags.Merge(variables, userTags);
    //
    //     var waitOptionType = variables.Get(AwsSpecialVariables.Ecs.WaitOption.Type);
    //     Guard.NotNullOrWhiteSpace(waitOptionType, "The wait option is required");
    //     if (waitOptionType != "waitUntilCompleted" && waitOptionType != "waitWithTimeout" && waitOptionType != "dontWait")
    //     {
    //         throw new CommandException($"The wait option has an invalid value '{waitOptionType}'. Expected one of: 'waitUntilCompleted', 'waitWithTimeout', 'dontWait'.");
    //     }
    //
    //     var waitOptionTimeoutMs = variables.GetInt32(AwsSpecialVariables.Ecs.WaitOption.Timeout);
    //     if (waitOptionType == "waitWithTimeout" && !waitOptionTimeoutMs.HasValue)
    //     {
    //         throw new CommandException("Wait option is 'waitWithTimeout' but timeout value is not set.");
    //     }
    //
    //     return new EcsCommandInputs(
    //                                 StackName: stackName,
    //                                 ClusterName: clusterName,
    //                                 ServiceName: serviceName,
    //                                 Tags: tags,
    //                                 WaitForComplete: waitOptionType != "dontWait",
    //                                 WaitTimeout: waitOptionType == "waitWithTimeout" ? TimeSpan.FromMilliseconds(waitOptionTimeoutMs!.Value) : null);
    // }
    //
    // record EcsCommandInputs(
    //     string StackName,
    //     string ClusterName,
    //     string ServiceName,
    //     List<KeyValuePair<string, string>> Tags,
    //     bool WaitForComplete,
    //     TimeSpan? WaitTimeout);

//     
//         var userTags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]") ?? [];
//         var tags = EcsDefaultTags.Merge(variables, userTags);
//     
//         var waitOptionType = variables.Get(AwsSpecialVariables.Ecs.WaitOption.Type);
//         Guard.NotNullOrWhiteSpace(waitOptionType, "The wait option is required");
//         if (waitOptionType != "waitUntilCompleted" && waitOptionType != "waitWithTimeout" && waitOptionType != "dontWait")
//         {
//             throw new CommandException($"The wait option has an invalid value '{waitOptionType}'. Expected one of: 'waitUntilCompleted', 'waitWithTimeout', 'dontWait'.");
//         }
//     
//         var waitOptionTimeoutMs = variables.GetInt32(AwsSpecialVariables.Ecs.WaitOption.Timeout);
//         if (waitOptionType == "waitWithTimeout" && !waitOptionTimeoutMs.HasValue)
//         {
//             throw new CommandException("Wait option is 'waitWithTimeout' but timeout value is not set.");
//         }
//     
         // return new EcsCommandInputs(string.Empty, string.Empty, string.Empty);
                                     // StackName: stackName,
                                     // ClusterName: clusterName,
                                     // ServiceName: serviceName,
                                     // Tags: tags,
                                     // WaitForComplete: waitOptionType != "dontWait",
                                     // WaitTimeout: waitOptionType == "waitWithTimeout" ? TimeSpan.FromMilliseconds(waitOptionTimeoutMs!.Value) : null)
             
     }


