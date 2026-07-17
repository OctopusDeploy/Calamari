using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class UpdateArgoCDAppDeploymentConfigTests
    {
        static GitCommitParameters DefaultCommitParameters => new("Summary", "Description", false);

        static ContainerImageReferenceAndHelmReference ImageRefWithHelmRef(string helmRef) => new(ContainerImageReference.FromReferenceString("nginx:latest"), helmRef);

        static ContainerImageReferenceAndHelmReference ImageRefWithoutHelmRef() => new(ContainerImageReference.FromReferenceString("nginx:latest"));

        [Test]
        public void HasStepBasedHelmValueReferences_WhenUseHelmReferenceFromContainerTrueAndHelmRefPresent_ReturnsTrue()
        {
            var config = new UpdateArgoCDAppDeploymentConfig(
                DefaultCommitParameters,
                new List<ContainerImageReferenceAndHelmReference> { ImageRefWithHelmRef("image.tag") },
                false);

            config.HasStepBasedHelmValueReferences().Should().BeTrue();
        }

        [Test]
        public void HasStepBasedHelmValueReferences_WhenNoImageReferencesHaveHelmRef_ReturnsFalse()
        {
            var config = new UpdateArgoCDAppDeploymentConfig(
                DefaultCommitParameters,
                new List<ContainerImageReferenceAndHelmReference> { ImageRefWithoutHelmRef() },
                false);

            config.HasStepBasedHelmValueReferences().Should().BeFalse();
        }

        [Test]
        public void HasStepBasedHelmValueReferences_WhenImageReferencesListIsEmpty_ReturnsFalse()
        {
            var config = new UpdateArgoCDAppDeploymentConfig(
                DefaultCommitParameters,
                new List<ContainerImageReferenceAndHelmReference>(),
                false);

            config.HasStepBasedHelmValueReferences().Should().BeFalse();
        }

        [Test]
        public void HasStepBasedHelmValueReferences_WhenSomeImageReferencesHaveHelmRef_AndUseHelmReferenceFromContainerTrue_ReturnsTrue()
        {
            var config = new UpdateArgoCDAppDeploymentConfig(
                DefaultCommitParameters,
                new List<ContainerImageReferenceAndHelmReference>
                {
                    ImageRefWithoutHelmRef(),
                    ImageRefWithHelmRef("image.tag")
                },
                false);

            config.HasStepBasedHelmValueReferences().Should().BeTrue();
        }

        [Test]
        public void HasStepBasedHelmValueReferences_WhenHelmRefIsEmptyString_ReturnsFalse()
        {
            var config = new UpdateArgoCDAppDeploymentConfig(
                DefaultCommitParameters,
                new List<ContainerImageReferenceAndHelmReference> { ImageRefWithHelmRef(helmRef: "") },
                false);

            config.HasStepBasedHelmValueReferences().Should().BeFalse();
        }

        [Test]
        public void CommitParameters_AreStoredCorrectly()
        {
            var commitParams = new GitCommitParameters("My Summary", "My Description", requiresPr: true);
            var config = new UpdateArgoCDAppDeploymentConfig(
                commitParams,
                new List<ContainerImageReferenceAndHelmReference>(),
                false);

            config.CommitParameters.Should().BeSameAs(commitParams);
            config.CommitParameters.Summary.Should().Be("My Summary");
            config.CommitParameters.Description.Should().Be("My Description");
            config.CommitParameters.RequiresPr.Should().BeTrue();
        }
    }
}
