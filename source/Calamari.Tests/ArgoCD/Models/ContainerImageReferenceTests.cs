using System;
using Calamari.ArgoCD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Models
{
    public class ContainerImageReferenceTests
    {
        
        [Theory]
        [TestCase("nginx", "nginx")]
        [TestCase("docker.io/nginx", "docker.io/nginx")]
        [TestCase("index.docker.io/nginx", "docker.io/nginx")]
        [TestCase("docker.io/nginx", "index.docker.io/nginx")]
        [TestCase("nginx", "index.docker.io/nginx")]
        [TestCase("nginx", "docker.io/nginx")]
        [TestCase("nginx:latest", "nginx:1.27")]
        public void IsMatch_ReturnsTrueWhenReferencesAreEquivalent(string reference1, string reference2)
        {
            var image1 = ContainerImageReference.FromReferenceString(reference1);
            var image2 = ContainerImageReference.FromReferenceString(reference2);

            var result = image2.IsMatch(image1);

            result.Should().BeTrue();
        }

        [Test]
        public void FromReferenceStringPreservesTagCasing()
        {
            var image1 = ContainerImageReference.FromReferenceString("NginX:CaseSensitive");
            image1.Tag.Should().Be("CaseSensitive");
            image1.ImageName.Should().Be("NginX".ToLowerInvariant());
        }
    
        [Theory]
        [TestCase("nginx", "custom-reg.io/nginx")]
        [TestCase("nginx:latest", "custom-reg.io/nginx:1.27")]
        public void IsMatch_WithCustomRepository_ReturnsTrueWhenReferencesAreEquivalent(string reference1, string reference2)
        {
            var image1 = ContainerImageReference.FromReferenceString(reference1, "custom-reg.io");
            var image2 = ContainerImageReference.FromReferenceString(reference2);

            var result = image2.IsMatch(image1);

            result.Should().BeTrue();
        }

        [Theory]
        [TestCase("nginx", "nginx")]
        [TestCase("nginx:latest", "nginx:1.27")]
        public void IsMatch_WithBothHavingCustomRepository_ReturnsTrueWhenImagesMatch(string reference1, string reference2)
        {
            var image1 = ContainerImageReference.FromReferenceString(reference1, "custom-reg.io");
            var image2 = ContainerImageReference.FromReferenceString(reference2, "custom-reg.io");

            var result = image2.IsMatch(image1);

            result.Should().BeTrue();
        }

        [Theory]
        [TestCase("nginx", "nginx")]
        [TestCase("nginx:latest", "nginx:1.27")]
        [TestCase("docker.io/nginx", "nginx")]
        [TestCase("index.docker.io/nginx", "nginx")]
        public void IsMatch_WithMismatchedCustomRegistry_ReturnsFalse(string reference1, string reference2)
        {
            var image1 = ContainerImageReference.FromReferenceString(reference1);
            var image2 = ContainerImageReference.FromReferenceString(reference2, "custom-reg.io");

            var result = image2.IsMatch(image1);

            result.Should().BeFalse();
        }

        [Test]
        public void IsTagChange_ReturnsTrueWithDifferentTags()
        {
            var image1 = ContainerImageReference.FromReferenceString("nginx:latest");
            var image2 = ContainerImageReference.FromReferenceString("nginx:1.27");

            var result = image2.IsTagChange(image1);

            result.Should().BeTrue();
        }

        [Theory]
        [TestCase("nginx", "busybox")]
        [TestCase("nginx", "my-custom.io/nginx")]
        public void IsTagChange_ReturnsFalseWhenReferencesAreNotEquivalent(string reference1, string reference2)
        {
            var image1 = ContainerImageReference.FromReferenceString(reference1)!;
            var image2 = ContainerImageReference.FromReferenceString(reference2)!;

            var result = image2.IsTagChange(image1);

            result.Should().BeFalse();
        }

        [Theory]
        [TestCase("nginx", "nginx")]
        [TestCase("docker.io/nginx", "docker.io/nginx")]
        [TestCase("index.docker.io/nginx", "docker.io/nginx")]
        [TestCase("docker.io/nginx", "index.docker.io/nginx")]
        [TestCase("nginx", "index.docker.io/nginx")]
        [TestCase("nginx", "docker.io/nginx")]
        public void IsTagChange_ReturnsFalseWhenTagsAreTheSame(string reference1, string reference2)
        {
            var image1 = ContainerImageReference.FromReferenceString(reference1);
            var image2 = ContainerImageReference.FromReferenceString(reference2);

            var result = image2.IsTagChange(image1);

            result.Should().BeFalse();
        }

        [Test]
        public void WithTag_WithNoRepository_ReturnsImageWithTag()
        {
            var image = ContainerImageReference.FromReferenceString("nginx:latest");
            var result = image.WithTag("1.27");

            result.Should().Be("nginx:1.27");
        }

        [Test]
        public void WithTag_WithRepository_ReturnsRepositoryImageWithTag()
        {
            var image = ContainerImageReference.FromReferenceString("docker.io/nginx:latest");

            var result = image.WithTag("1.27");

            result.Should().Be("docker.io/nginx:1.27");
        }

        [Test]
        public void WithTag_WithDefaultRegistry_ReturnsImageWithTag()
        {
            var image = ContainerImageReference.FromReferenceString("nginx:latest", "docker.io");

            var result = image.WithTag("1.27");

            result.Should().Be("nginx:1.27");
        }

        [Theory]
        [TestCase("nginx", "nginx")]
        [TestCase("docker.io/nginx", "docker.io/nginx")]
        [TestCase("index.docker.io/nginx:latest", "docker.io/nginx:latest")]
        [TestCase("nginx:latest", "nginx:latest")]
        public void ToString_ReturnsCorrectlyFormattedImageNameBasedOnComponents(string reference, string expected)
        {
            var image = ContainerImageReference.FromReferenceString(reference);

            var result = image.ToString();

            result.Should().Be(expected);
        }

        [Test]
        public void FromReferenceString_HonoursPortNumbersForRegistries()
        {
            var image = ContainerImageReference.FromReferenceString("custom-registry.com:8086/nginx:latest");

            image.Tag.Should().Be("latest");
            image.ImageName.Should().Be("nginx");
            image.Registry.Should().Be("custom-registry.com:8086");
        }

        [Test]
        public void FromReferenceString_WithEmptyString_ThrowsArgumentException()
        {
            Action act = () => ContainerImageReference.FromReferenceString(string.Empty);

            act.Should()
               .Throw<ArgumentException>()
               .Where(e => e.ParamName == "containerImageReference");
        }

        [Test]
        public void FromReferenceString_WithWhiteSpace_ThrowsArgumentException()
        {
            Action act = () => ContainerImageReference.FromReferenceString("custom-registry.com /nginx : latest");

            act.Should()
               .Throw<ArgumentException>()
               .Where(e => e.ParamName == "containerImageReference");
        }
    }
}
