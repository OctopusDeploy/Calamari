using Calamari.Aws.Deployment;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class CloudFormationStackNameGeneratorTests
    {
        [TestCase("Environments-1", "Tenants-1", "cf-environments1-tenants1")]
        [TestCase("Environments-1", null, "cf-environments1-untenanted")]
        public void GetStackName(string environmentId, string tenantId, string expected)
        {
            var variables = new CalamariVariables
            {
                { "Octopus.Environment.Id", environmentId },
                { "Octopus.Deployment.Tenant.Id", tenantId }
            };
            CloudFormationStackNameGenerator.GetStackName(variables).Should().Be(expected);
        }
        
        [Test]        
        public void GetStackNameWithExtras()
        {
            var variables = new CalamariVariables
            {
                { "Octopus.Environment.Id", "Environments-1" },
                { "Octopus.Deployment.Tenant.Id", "Tenants-1" }
            };
            CloudFormationStackNameGenerator.GetStackName(variables, "bucket").Should().Be("cf-bucket-environments1-tenants1");
        }
        
        [Test]        
        public void GetStackNameWithMultipleExtras()
        {
            var variables = new CalamariVariables
            {
                { "Octopus.Environment.Id", "Environments-1" },
                { "Octopus.Deployment.Tenant.Id", "Tenants-1" }
            };
            CloudFormationStackNameGenerator.GetStackName(variables, "bucket", "second-bucket").Should().Be("cf-bucket-secondBucket-environments1-tenants1");
        }

        [Test]
        public void GetStackNameMaxLength()
        {
            var variables = new CalamariVariables
            {
                { "Octopus.Environment.Id", "Environments-1" },
                { "Octopus.Deployment.Tenant.Id", "Tenants-1" }
            };
            CloudFormationStackNameGenerator.GetStackName(variables, new string('a', 128)).Should().HaveLength(128).And.NotContain("environments1").And.NotContain("tenants1");
        }
    }
}