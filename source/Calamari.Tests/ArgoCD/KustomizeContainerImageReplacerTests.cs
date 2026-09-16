using System;
using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class KustomizeContainerImageReplacerTests
    {
        [Theory]
        [TestCase("docker.io/nginx", "1.28.0")]
        [TestCase("nginx", "1.28.0")]
        [TestCase("us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld", "v2")]
        public void ReturnsSameImageBaseAsInYaml(string originalName, string newTag)
        {
            var inputYaml = $@"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: {originalName}
";
            var expectedYaml = $@"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: {originalName}
  newTag: ""{newTag}""
";

            var log = new InMemoryLog();
            var replacer = new KustomizeContainerImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, false, log);

            var update = new List<ContainerImageReferenceAndHelmReference>
            {
                new(ContainerImageReference.FromReferenceString($"{originalName}:{newTag}", ArgoCDConstants.DefaultContainerRegistry))
            };

            var result = replacer.UpdateImages(update);

            result.UpdatedContents.Should().Be(expectedYaml);
            result.UpdatedImageReferences.Should().ContainSingle().Which.Should().Be($"{originalName}:{newTag}");
        }

        [TestFixture]
        public class DeterminePatchTypeFromFileTests
        {
            [TestFixture]
            public class IsKustomizationResourceTests
            {
                [Test]
                public void IsKustomizationResource_WithKustomizeApiVersionAndKind_ReturnsTrue()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
images:
- name: nginx
  newTag: 1.25";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeTrue();
                }

                [Test]
                public void IsKustomizationResource_WithComponentApiVersionAndKind_ReturnsTrue()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1alpha1
kind: Component
resources:
- deployment.yaml";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeTrue();
                }

                [Test]
                public void IsKustomizationResource_WithDifferentApiVersion_ReturnsFalse()
                {
                    const string content = @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void IsKustomizationResource_WithDifferentKind_ReturnsFalse()
                {
                    const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: CustomResource
metadata:
  name: test";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void IsKustomizationResource_InvalidYaml_ReturnsFalse()
                {
                    const string content = @"invalid: yaml: [unclosed";
                    var result = KustomizationValidator.IsKustomizationResource(content);
                    result.Should().BeFalse();
                }

                [Test]
                public void IsKustomizationResource_EmptyContent_ReturnsFalse()
                {
                    var result = KustomizationValidator.IsKustomizationResource("");
                    result.Should().BeFalse();
                }
            }

        }
    }
}