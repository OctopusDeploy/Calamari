#if AWS
using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class CloudFormationVariableReplacementFixture
    {
        string StackName;
        string ReplacedName;

        public CloudFormationVariableReplacementFixture()
        {
            var unique = Guid.NewGuid().ToString("N").ToLowerInvariant();
            StackName = $"calamariteststack{unique}";
            ReplacedName = $"calamaritestreplaced{unique}";
        }

        [Test]
        public async Task CreateCloudFormationWithStructuredVariableReplacement()
        {
            var cloudFormationFixtureHelpers = new CloudFormationFixtureHelpers();
            var templateFilePath = cloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(StackName));
            
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Package.EnabledFeatures, "Octopus.Features.JsonConfigurationVariables");
            variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, templateFilePath);
            variables.Set($"Resources:{StackName}:Properties:BucketName", ReplacedName);
            
            try
            {
                cloudFormationFixtureHelpers.DeployTemplate(StackName, templateFilePath, variables);
                await cloudFormationFixtureHelpers.ValidateS3BucketExists(ReplacedName);
            }
            finally
            {
                cloudFormationFixtureHelpers.CleanupStack(StackName);
            }
        }
    }
}
#endif