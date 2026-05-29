using System;
using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Integration.Ecs;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions;

// Currently a thin wrapper over existing template deployment process with the goal to swapping it out for native ECS API solution in the future.
public class DeployEcsServiceConvention: IInstallConvention
{
    readonly DeployAwsCloudFormationConvention encapsulatedConvention;

    public DeployEcsServiceConvention(DeployEcsCommandInputs commandInputs, AwsEnvironmentGeneration awsEnvironment, ILog log, IVariables variables) 
    {
        var stackEventLogger = new StackEventLogger(log);

        encapsulatedConvention = new DeployAwsCloudFormationConvention(ClientFactory, 
            TemplateFactory, 
            stackEventLogger, 
            StackProvider, 
            commandInputs.ShouldWaitForDeploymentCompletion,
            commandInputs.CfStackName,
            awsEnvironment,
            log,
            commandInputs.WaitOption.GetTimeoutSpan()
            );
        return;

        StackArn StackProvider(RunningDeployment _) => commandInputs.CfStackArn;
        IAmazonCloudFormation ClientFactory() => ClientHelpers.CreateCloudFormationClient(awsEnvironment);
        
        ICloudFormationRequestBuilder TemplateFactory()
        {
            return new CloudFormationTemplate(() => EcsDeployTemplateGenerator.GenerateTemplate(commandInputs),
                new EmptyTemplateInputs<Parameter>(),
                commandInputs.CfStackName,
                ["CAPABILITY_NAMED_IAM"],
                true,
                null,
                commandInputs.Tags,
                commandInputs.CfStackArn,
                ClientFactory,
                variables);
        }
        
    }
    
    public void Install(RunningDeployment deployment)
    {
        encapsulatedConvention.Install(deployment);
    }

}

