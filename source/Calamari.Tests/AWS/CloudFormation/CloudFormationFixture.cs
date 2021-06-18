#if AWS
using Calamari.Common.Plumbing.Variables;
using System.Threading.Tasks;
using System;
using System.IO;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture, Explicit]
    public class CloudFormationFixture
    {
        private string StackName;

        public CloudFormationFixture()
        {
            StackName = $"calamariteststack{Guid.NewGuid().ToString("N").ToLowerInvariant()}";
        }

        [Test]
        public async Task CreateOrUpdateCloudFormationTemplate()
        {
            var templateFilePath = CloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(StackName));

            try
            {
                CloudFormationFixtureHelpers.DeployTemplate(StackName, templateFilePath, new CalamariVariables());

                await CloudFormationFixtureHelpers.ValidateStackExists(StackName, true);

                await CloudFormationFixtureHelpers.ValidateS3BucketExists(StackName);
            }
            finally
            {
                CloudFormationFixtureHelpers.CleanupStack(StackName);
            }
        }

        [Test]
        public async Task DeleteCloudFormationStack()
        {
            var templateFilePath = CloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(StackName));

            CloudFormationFixtureHelpers.DeployTemplate(StackName, templateFilePath, new CalamariVariables());
            CloudFormationFixtureHelpers.DeleteStack(StackName);
            await CloudFormationFixtureHelpers.ValidateStackExists(StackName, false);
        }

    }
}
#endif