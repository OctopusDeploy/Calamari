#if AWS
using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture, Explicit]
    public class CloudFormationVariableReplacementFixture
    {
        private const string StackName = "octopuse2ecftests";
        private const string ReplacedName = "octopuse2e-replaced";

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task CreateCloudFormationWithStructuredVariableReplacement()
        {
            var template = CloudFormationFixture.GetBasicS3Template(StackName);
            
            var templateFilePath = Path.GetTempFileName();
            File.WriteAllText(templateFilePath, template);
            
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Package.EnabledFeatures, "Octopus.Features.JsonConfigurationVariables");
            variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, templateFilePath);
            variables.Set($"Resources:{StackName}:Properties:BucketName", ReplacedName);
            
            try
            {
                CloudFormationFixture.DeployTemplate(StackName, templateFilePath, variables);

                await CloudFormationFixture.ValidateCloudFormation(async (client) =>
                {
                    Func<IAmazonCloudFormation> clientFactory = () => client;
                    var stackStatus = await clientFactory.StackExistsAsync(new StackArn(StackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                    stackStatus.Should().Be(Aws.Deployment.Conventions.StackStatus.Completed);
                });

                CloudFormationFixture.ValidateS3(client =>
                {
                    // Bucket with replaced name was created successfully
                    client.GetBucketLocation(ReplacedName);
                });
            }
            finally
            {
                try
                {
                    CloudFormationFixture.DeleteStack(StackName);
                }
                catch (Exception e)
                {
                    Log.Error($"Error occurred while attempting to delete stack {StackName} -> {e}." + 
                              $"{Environment.NewLine} Test resources may not have been deleted, please check the AWS console for the status of the stack.");
                }
            }
        }
    }
}
#endif