#if AWS
using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
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
            var templateFilePath = CloudFormationFixtureHelpers.WriteTemplateFile(CloudFormationFixtureHelpers.GetBasicS3Template(StackName));
            
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.Package.EnabledFeatures, "Octopus.Features.JsonConfigurationVariables");
            variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, templateFilePath);
            variables.Set($"Resources:{StackName}:Properties:BucketName", ReplacedName);
            
            try
            {
                CloudFormationFixtureHelpers.DeployTemplate(StackName, templateFilePath, variables);
                await CloudFormationFixtureHelpers.ValidateS3BucketExists(ReplacedName);
            }
            finally
            {
                CloudFormationFixtureHelpers.CleanupStack(StackName);
            }
        }
    }
}
#endif