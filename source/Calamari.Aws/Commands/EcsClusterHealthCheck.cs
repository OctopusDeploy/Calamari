using System;
using System.Collections.Generic;
using Amazon.ECS;
using Calamari.Aws.Deployment;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.Ecs;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Commands;

[Command("aws-ecs-health-check", Description = "Run a health check on an AWS ECS Cluster")]
public class HealthCheckCommand(ILog log, IVariables variables) : Command
{
    public override int Execute(string[] commandLineArguments)
    {
        var clusterName = variables.Get(AwsSpecialVariables.Ecs.ClusterName);
        log.Info($"Checking health of  ECS Cluster: {clusterName}");
        
        var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();

        var conventions = new List<IConvention>
        {
            new LogAwsUserInfoConvention(environment),
            new EcsClusterHealthCheckConvention(clusterName, ClientFactory, log)
        };
        var conventionRunner = new ConventionProcessor(new RunningDeployment(variables), conventions, log);
        conventionRunner.RunConventions();
        
        return 0;
        
        IAmazonECS ClientFactory() => EcsClientFactoryHelper.Create(environment);
    }
}