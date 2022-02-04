using Calamari.Common.Features.Discovery;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Discovery
{
    [TestFixture]
    public class TargetDiscoveryScopeFixture
    {
        private const string scopeSpace = "scope-space";
        private const string scopeTenant = "scope-tenant";
        private const string scopeProject = "scope-project";
        private const string scopeEnvironment = "scope-environment";
        private const string scopeRole1 = "scope-role-1";
        private const string scopeRole2 = "scope-role-2";
        private static readonly string[] scopeRoles = new string[] { scopeRole1, scopeRole2 };
        private TargetDiscoveryScope sut = new TargetDiscoveryScope(
            scopeSpace, scopeEnvironment, scopeProject, scopeTenant, scopeRoles, "WorkerPool-1");

        [Test]
        public void Match_ShouldFail_IfEnvironmentTagIsMissing()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: null,
                role: scopeRole1,
                project: null,
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Missing environment tag. Match requires 'octopus-environment' tag with value 'scope-environment'.");
        }

        [Test]
        public void Match_ShouldFail_IfEnvironmentTagIsMismatched()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: "wrong-environment",
                role: scopeRole1,
                project: null,
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Mismatched environment tag. Match requires 'octopus-environment' tag with value 'scope-environment', but found 'wrong-environment'.");
        }

        [Test]
        public void Match_ShouldFail_IfRoleTagIsMissing()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: null,
                project: null,
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Missing role tag. Match requires 'octopus-role' tag with value from ['scope-role-1', 'scope-role-2'].");
        }

        [Test]
        public void Match_ShouldFail_IfRoleTagIsMismatched()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: "wrong-role",
                project: null,
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Mismatched role tag. Match requires 'octopus-role' tag with value from ['scope-role-1', 'scope-role-2'], but found 'wrong-role'.");
        }

        [Test]
        public void Match_ShouldSucceed_IfRoleAndEnvironmentTagsMatchAndNoOptionalTagsArePresent()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: scopeRole1,
                project: null,
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Test]
        public void Match_ShouldCaptureMatchingRole_WhenMatchSucceeds()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: scopeRole2,
                project: null,
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.Role.Should().Be(scopeRole2);
        }

        [Test]
        public void Match_ShouldFail_IfMismatchedProjectTagIsPresent()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: scopeRole1,
                project: "wrong-project",
                space: null,
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Mismatched project tag. Optional 'octopus-project' tag must match 'scope-project' if present, but is 'wrong-project'.");
        }

        [Test]
        public void Match_ShouldFail_IfMismatchedSpaceTagIsPresent()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: scopeRole1,
                project: null,
                space: "wrong-space",
                tenant: null);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Mismatched space tag. Optional 'octopus-space' tag must match 'scope-space' if present, but is 'wrong-space'.");
        }

        [Test]
        public void Match_ShouldFail_IfMismatchedTenantTagIsPresent()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: scopeRole1,
                project: null,
                space: null,
                tenant: "wrong-tenant");

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Reason.Should().Be("Mismatched tenant tag. Optional 'octopus-tenant' tag must match 'scope-tenant' if present, but is 'wrong-tenant'.");
        }

        [Test]
        public void Match_ShouldSucceed_IfOptionalTagsAllMatchScope()
        {
            // Arrange
            var foundTags = new TargetTags(
                environment: scopeEnvironment,
                role: scopeRole1,
                project: scopeProject,
                space: scopeSpace,
                tenant: scopeTenant);

            // Act
            var result = sut.Match(foundTags);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
    }
}