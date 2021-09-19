#if AWS
using Calamari.Common.Plumbing.Variables;
using System.Threading.Tasks;
using System;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture]
    [Ignore("Temporary disablement 2021-09-20")]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class CloudFormationFixture
    {
        string GenerateStackName() => $"calamariteststack{Guid.NewGuid().ToString("N").ToLowerInvariant()}";

        [Test]
        public async Task CreateOrUpdateCloudFormationTemplate()
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers();
            var stackName = GenerateStackName();
            var templateFilePath = cloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(stackName));

            try
            {
                cloudFormationFixtureHelpers.DeployTemplate(stackName, templateFilePath, new CalamariVariables());

                await cloudFormationFixtureHelpers.ValidateStackExists(stackName, true);

                await cloudFormationFixtureHelpers.ValidateS3BucketExists(stackName);
            }
            finally
            {
                cloudFormationFixtureHelpers.CleanupStack(stackName);
            }
        }

        [Test]
        public async Task CreateOrUpdateCloudFormationS3Template()
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers("us-east-1");
            var stackName = GenerateStackName();
            
            try
            {
                cloudFormationFixtureHelpers.DeployTemplateS3(stackName, new CalamariVariables());

                await cloudFormationFixtureHelpers.ValidateStackExists(stackName, true);
            }
            finally
            {
                cloudFormationFixtureHelpers.CleanupStack(stackName);
            }
        }

        [Test]
        public async Task DeleteCloudFormationStack()
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers();
            var stackName = GenerateStackName();
            var templateFilePath = cloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(stackName));

            cloudFormationFixtureHelpers.DeployTemplate(stackName, templateFilePath, new CalamariVariables());
            cloudFormationFixtureHelpers.DeleteStack(stackName);
            await cloudFormationFixtureHelpers.ValidateStackExists(stackName, false);
        }

    }
}
#endif