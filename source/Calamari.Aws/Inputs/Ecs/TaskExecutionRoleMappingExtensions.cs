using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace Calamari.Aws.Inputs.Ecs;

public static class TaskExecutionRoleMappingExtensions
{
    const string DefaultTaskExecutionPolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy";
    
    public static string MapTaskExecutionRoleArn(this DeployEcsCommandInputs inputs, Construct scope)
    {
        if (!string.IsNullOrEmpty(inputs.TaskExecutionRole))
        {
            return inputs.TaskExecutionRole;
        }
        
        var policyArnParam = new CfnParameter(scope,
                                              "AmazonECSTaskExecutionRolePolicyArn",
                                              new CfnParameterProps
                                              {
                                                  Type = "String",
                                                  Default = DefaultTaskExecutionPolicyArn
                                              });

        var role = new CfnRole(scope,
                               inputs.FallbackTaskExecutionRoleName,
                               new CfnRoleProps
                               {
                                   Path = "/",
                                   ManagedPolicyArns = [policyArnParam.ValueAsString],
                                   AssumeRolePolicyDocument = new PolicyDocument(new PolicyDocumentProps
                                   {
                                       Statements =
                                       [
                                           new PolicyStatement(new PolicyStatementProps
                                           {
                                               Effect = Effect.ALLOW,
                                               Principals = [new ServicePrincipal("ecs-tasks.amazonaws.com")],
                                               Actions = ["sts:AssumeRole"]

                                           })
                                       ]
                                   })
                               });
                                               

        return role.Ref;

    }
}