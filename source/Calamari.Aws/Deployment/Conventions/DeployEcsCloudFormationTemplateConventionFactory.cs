using System;
using Calamari.Aws.Inputs;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Aws.Deployment.Conventions;

public class DeployEcsCloudFormationTemplateConventionFactory(DeployEcsCommandInputs commandInputs, /*AwsEnvironmentGeneration awsEnvironment,*/ ILog log)
{
    public DeployAwsCloudFormationConvention GetDeployConvention() => BuildCloudFormationDeploymentConvention();
        
    DeployAwsCloudFormationConvention BuildCloudFormationDeploymentConvention()
    {
        
        var template = EcsDeployTemplateGenerator.GenerateTemplate(commandInputs);

        // new DeployAwsCloudFormationConvention(ClientFactory,
        //                                       TemplateFactory,
        //                                       new StackEventLogger(log),
        //                                       _ => stackArn,
        //                                       _ => null,
        //                                       inputs.WaitForComplete,
        //                                       inputs.StackName,
        //                                       environment,
        //                                       log,
        //                                       inputs.WaitTimeout),
        
        if (log == null)
        {
            Console.WriteLine("Take that \"this can be made static\' warning");
        }

        return null;
        
        
        // IAmazonCloudFormation ClientFactory() => ClientHelpers.CreateCloudFormationClient(awsEnvironment);

        // ICloudFormationRequestBuilder TemplateFactory() =>
        //     CloudFormationTemplate.Create(templateResolver,
        //                                   templateFile,
        //                                   templateParameterFile,
        //                                   filesInPackage: false,
        //                                   fileSystem,
        //                                   variables,
        //                                   inputs.StackName,
        //                                   capabilities: ["CAPABILITY_NAMED_IAM"],
        //                                   disableRollback: false,
        //                                   roleArn: null,
        //                                   tags: inputs.Tags,
        //                                   stackArn,
        //                                   ClientFactory);
    }
    
}

