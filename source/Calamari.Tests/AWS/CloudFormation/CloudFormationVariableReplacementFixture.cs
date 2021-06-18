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
        private string StackName;
        private string ReplacedName;

        public CloudFormationVariableReplacementFixture()
        {
            var unique = Guid.NewGuid().ToString("N").ToLowerInvariant();
            StackName = $"calamariteststack{unique}";
            ReplacedName = $"calamaritestreplaced{unique}";
        }

        [Test]
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