using System.Collections.Generic;
using Calamari;
using Calamari.Common;
using Calamari.Tests.Shared;
using Newtonsoft.Json;
using Octopus.Data.Model;
using Sashimi.Aws.Accounts;
using Sashimi.Server.Contracts;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Aws.Tests
{
    public static class TestExtensions
    {
        public static TestActionHandlerContext<TCalamariProgram> WithAwsAccount<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context)
            where TCalamariProgram : CalamariFlavourProgram
        {
            var accountDetails = new AmazonWebServicesAccountDetails
            {
                AccessKey = ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey),
                SecretKey = ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey)
                    .ToSensitiveString()
            };

            foreach (var contributeVariable in accountDetails.ContributeVariables())
            {
                context.Variables.Add(contributeVariable.Name, contributeVariable.Value);

            }

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithStack<TCalamariProgram>(
            this TestActionHandlerContext<TCalamariProgram> context, string stackName)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.StackName, stackName);

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithAwsRegion<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string region)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(SpecialVariables.Action.Aws.AwsRegion, region);

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithAwsTemplatePackageSource<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string template, string templateParameters)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(AwsSpecialVariables.Action.Aws.TemplateSource, AwsSpecialVariables.Action.Aws.TemplateSourceOptions.Package);
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.Template, template);
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.TemplateParametersRaw, templateParameters);

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithAwsTemplateInlineSource<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string template, string templateParameters)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(AwsSpecialVariables.Action.Aws.TemplateSource, AwsSpecialVariables.Action.Aws.TemplateSourceOptions.Inline);
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.Template, template);
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.TemplateParameters, templateParameters);

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithStackRole<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string stackRole)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.RoleArn, stackRole);
            context.Variables.Add("Octopus.Action.Aws.AssumeRole", bool.FalseString);
            context.Variables.Add("Octopus.Action.Aws.AssumedRoleArn", string.Empty);
            context.Variables.Add("Octopus.Action.Aws.AssumedRoleSession", string.Empty);

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithIamCapabilities<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, IEnumerable<string> capabilities)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(SpecialVariables.Action.Aws.IamCapabilities, JsonConvert.SerializeObject(capabilities));

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithCloudFormationChangeSets<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, bool generateName = true, bool deferExecution = false)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(KnownVariables.Action.EnabledFeatures, AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Feature);
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Generate, generateName.ToString());
            context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Defer, deferExecution.ToString());

            return context;
        }


    }
}