using System;
using System.Threading.Tasks;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions;

public class CheckEcsStackNotInProgressConvention : IInstallConvention
{
    readonly AwsEnvironmentGeneration environment;
    readonly string stackName;

    public CheckEcsStackNotInProgressConvention(AwsEnvironmentGeneration environment, string stackName)
    {
        this.environment = environment;
        this.stackName = stackName;
    }

    public void Install(RunningDeployment deployment) =>
        InstallAsync().GetAwaiter().GetResult();

    async Task InstallAsync()
    {
        var clientFactory = () => ClientHelpers.CreateCloudFormationClient(environment);
        var status = await clientFactory.StackExistsAsync(new StackArn(stackName), StackStatus.DoesNotExist);
        if (status == StackStatus.InProgress)
        {
            throw new CommandException($"Unable to deploy. The CloudFormation stack named \"{stackName}\" is in an \"IN_PROGRESS\" state. Please try again later.");
        }
    }
}
