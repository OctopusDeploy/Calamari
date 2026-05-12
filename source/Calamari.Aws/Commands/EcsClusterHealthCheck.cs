using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;

namespace Calamari.Aws.Commands;

[Command("aws-ecs-health-check", Description = "Run a health check on an AWS ECS Cluster")]
public class HealthCheckCommand : Command
{
    public override int Execute(string[] commandLineArguments)
    {
        return 0;
    }
}