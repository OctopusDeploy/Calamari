namespace Calamari.AzureResourceGroup.Tests
{ public class DeploymentNameFixture
    {
        [Fact]
        public void GivenShortStepName_Then_Can_Generate_Deployment_Name_Appropriately()
        {
            // Given / When
            var deploymentName = DeploymentName.FromStepName("StepA");

            // Then
            deploymentName.Should()
                          .HaveLength(38)
                          .And
                          .StartWith("stepa-");
        }

        [Fact]
        public void GivenNormalStepName_Then_Can_Generate_Deployment_Name_Appropriately()
        {
            // Given / When
            var deploymentName = DeploymentName.FromStepName("1234567890123456789012345678901"); // 31 chars

            // Then
            deploymentName.Should().HaveLength(64)
                          .And
                          .StartWith("1234567890123456789012345678901-");
        }

        [Fact]
        public void GivenLongStepName_Then_Can_Generate_Deployment_Name_Appropriately()
        {
            // Given / When
            var deploymentName = DeploymentName.FromStepName("1234567890123456789012345678901234567890"); // 40 chars

            // Then
            deploymentName.Should()
                          .HaveLength(64)
                          .And
                          .StartWith("1234567890123456789012345678901-"); // 27 Characters Allow
        }
    }
}