using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD;

[TestFixture]
public class InlineJson6902ImageReplacerTest
{
    readonly ILog log = new InMemoryLog();
    
    [Test]
    public void ProcessInlineJson6902Patches_WithInlinePatches_UpdatesImages()
    {
        const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  patch: |-
    - op: replace
      path: /spec/template/spec/containers/0/image
      value: nginx:1.21";

        var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
        };
        
        var replacer = new InlineJson6902ImageReplacer(content, "default-registry", log);
        var result = replacer.UpdateImages(imagesToUpdate);

        result.UpdatedImageReferences.Should().Contain("nginx:1.25");
        result.UpdatedContents.Should().Contain("nginx:1.25");
    }

    [Test]
    public void ProcessInlineJson6902Patches_WithNoMatches_ReturnsOriginalContent()
    {
        const string content = @"apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
patchesJson6902:
- target:
    kind: Deployment
    name: nginx-deployment
  path: deployment.json";

        var imagesToUpdate = new List<ContainerImageReferenceAndHelmReference>
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", "default-registry"))
        };
        
        var replacer = new InlineJson6902ImageReplacer(content, "default-registry", log);
        var result = replacer.UpdateImages(imagesToUpdate);

        result.UpdatedImageReferences.Should().BeEmpty();
        result.UpdatedContents.Should().Be(content);
    }
}