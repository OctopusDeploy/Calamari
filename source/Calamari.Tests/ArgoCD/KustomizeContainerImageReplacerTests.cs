using System;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class KustomizeContainerImageReplacerTests
    {
        static KustomizeContainerImageReplacer CreateReplacer(string content = "", bool updateKustomizePatches = true)
        {
            var log = Substitute.For<ILog>();
            return new KustomizeContainerImageReplacer(content, "default-registry", updateKustomizePatches, log);
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