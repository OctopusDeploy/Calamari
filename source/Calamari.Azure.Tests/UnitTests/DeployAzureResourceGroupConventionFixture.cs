using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Shared.Util;
using NUnit.Framework;

namespace Calamari.Azure.Tests.UnitTests
{
    [TestFixture]
    public class DeployAzureResourceGroupConventionFixture
    {
        [Test]
        public void GivenShortStepName_Then_Can_Generate_Deployment_Name_Appropriately()
        {
            // Given / When
            var deploymentName = GenerateDeploymentNameFromStepNameTestWrapper.TestGenerateDeploymentNameFromStepName("StepA");

            // Then
            Assert.That(deploymentName, Has.Length.LessThanOrEqualTo(64));
            Assert.That(deploymentName, Has.Length.EqualTo(38));
            Assert.That(deploymentName, Does.StartWith("stepa-"));
        }

        [Test]
        public void GivenNormalStepName_Then_Can_Generate_Deployment_Name_Appropriately()
        {
            // Given / When
            var deploymentName = GenerateDeploymentNameFromStepNameTestWrapper.TestGenerateDeploymentNameFromStepName("1234567890123456789012345678901"); // 31 chars

            // Then
            Assert.That(deploymentName, Has.Length.EqualTo(64));
            Assert.That(deploymentName, Does.StartWith("1234567890123456789012345678901-"));
        }

        [Test]
        public void GivenLongStepName_Then_Can_Generate_Deployment_Name_Appropriately()
        {
            // Given / When
            var deploymentName = GenerateDeploymentNameFromStepNameTestWrapper.TestGenerateDeploymentNameFromStepName("1234567890123456789012345678901234567890"); // 40 chars

            // Then
            Assert.That(deploymentName, Has.Length.EqualTo(64));
            Assert.That(deploymentName, Does.StartWith("1234567890123456789012345678901-")); // 27 Characters Allow
        }
    }
    
    public class GenerateDeploymentNameFromStepNameTestWrapper : DeployAzureResourceGroupConvention
    {
        public static string TestGenerateDeploymentNameFromStepName(string stepName)
        {
            return GenerateDeploymentNameFromStepName(stepName);
        }

        public GenerateDeploymentNameFromStepNameTestWrapper(ITemplateService templateService, IResourceGroupTemplateNormalizer parameterNormalizer) : base(templateService, parameterNormalizer)
        {
        }
    }
}