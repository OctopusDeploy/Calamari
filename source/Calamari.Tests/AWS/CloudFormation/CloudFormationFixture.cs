using Calamari.Common.Plumbing.Variables;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Calamari.Testing.Helpers;
using NUnit.Framework;
using static Calamari.Aws.Deployment.AwsSpecialVariables.CloudFormation;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class CloudFormationFixture
    {
        string GenerateStackName() => $"calamaricloudformation{Guid.NewGuid():N}";

        const string StackTagsRaw = "[{\"Key\":\"myTagKey\",\"Value\":\"myTagValue\"},{\"Key\":\"anotherTagKey\",\"Value\":\"anotherTagValue\"}]";

        static Dictionary<string, string> StackTags = new Dictionary<string, string>()
        {
            { "myTagKey", "myTagValue" },
            { "anotherTagKey", "anotherTagValue" },
        };

        [TestCase(false)]
        [TestCase(true)]
        public async Task CreateOrUpdateCloudFormationTemplate(bool isChangesetEnabled)
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers();
            var stackName = GenerateStackName();
            var templateFilePath = cloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(stackName));

            try
            {
                await cloudFormationFixtureHelpers.DeployTemplate(stackName, templateFilePath, CreateStackVariables(isChangesetEnabled));

                await cloudFormationFixtureHelpers.ValidateStackExists(stackName, true);
                await cloudFormationFixtureHelpers.ValidateStackTags(stackName, StackTags);
                await cloudFormationFixtureHelpers.ValidateS3BucketExists(stackName);
            }
            finally
            {
                await cloudFormationFixtureHelpers.CleanupStack(stackName);
            }
        }

        [Test]
        public async Task CreateOrUpdateCloudFormationS3Template()
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers("us-east-1");
            var stackName = GenerateStackName();

            try
            {
                await cloudFormationFixtureHelpers.DeployTemplateS3(stackName, CreateStackVariables(false));

                await cloudFormationFixtureHelpers.ValidateStackExists(stackName, true);
                await cloudFormationFixtureHelpers.ValidateStackTags(stackName, StackTags);
            }
            finally
            {
                await cloudFormationFixtureHelpers.CleanupStack(stackName);
            }
        }

        [Test]
        public async Task DeleteCloudFormationStack()
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers();
            var stackName = GenerateStackName();
            var templateFilePath = cloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(stackName));

            await cloudFormationFixtureHelpers.DeployTemplate(stackName, templateFilePath, new CalamariVariables());
            await cloudFormationFixtureHelpers.DeleteStack(stackName);
            await cloudFormationFixtureHelpers.ValidateStackExists(stackName, false);
        }

        private IVariables CreateStackVariables(bool enableChangeset)
        {
            var stackVariables = new CalamariVariables()
            {
                { Tags, StackTagsRaw }
            };
            if (enableChangeset)
            {
                stackVariables.Add(KnownVariables.Package.EnabledFeatures, Changesets.Feature);
                stackVariables.Add(Changesets.Name, $"calamarichangeset{Guid.NewGuid():N}");
            }

            return stackVariables;
        }
    }
}