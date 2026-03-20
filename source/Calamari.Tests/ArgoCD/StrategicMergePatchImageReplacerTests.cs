using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD;

[TestFixture]
public class StrategicMergePatchImageReplacerTests
{
    readonly List<ContainerImageReferenceAndHelmReference> imagesToUpdate = new()
    {
        new ContainerImageReferenceAndHelmReference(ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry)),
        new ContainerImageReferenceAndHelmReference(ContainerImageReference.FromReferenceString("my-registry.com/busybox:stable", ArgoCDConstants.DefaultContainerRegistry))
    };

    readonly ILog log = new InMemoryLog();

    [Test]
    public void UpdateImages_WithSimpleDeploymentPatch_UpdatesContainerImage()
    {
        const string inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
        ports:
        - containerPort: 80
";

        const string expectedYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25
        ports:
        - containerPort: 80
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().NotBeNull();
        result.UpdatedContents.Trim().Replace("\r\n", "\n").Should().Be(expectedYaml.Trim().Replace("\r\n", "\n"));
        result.UpdatedImageReferences.Count.Should().Be(1);
        result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
    }

    [Test]
    public void UpdateImages_WithInitContainers_UpdatesInitContainerImage()
    {
        const string inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: app-deployment
spec:
  template:
    spec:
      initContainers:
      - name: init-busybox
        image: my-registry.com/busybox:1.0
      containers:
      - name: app
        image: app:latest
";

        const string expectedYaml = @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: app-deployment
spec:
  template:
    spec:
      initContainers:
      - name: init-busybox
        image: my-registry.com/busybox:stable
      containers:
      - name: app
        image: app:latest
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().NotBeNull();
        result.UpdatedContents.Trim().Replace("\r\n", "\n").Should().Be(expectedYaml.Trim().Replace("\r\n", "\n"));
        result.UpdatedImageReferences.Count.Should().Be(1);
        result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_WithMultipleContainers_UpdatesMatchingImages()
    {
        const string inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: multi-container-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
      - name: busybox
        image: my-registry.com/busybox:1.0
      - name: redis
        image: redis:6.0
";

        const string expectedYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: multi-container-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25
      - name: busybox
        image: my-registry.com/busybox:stable
      - name: redis
        image: redis:6.0
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().NotBeNull();
        result.UpdatedContents.Trim().Replace("\r\n", "\n").Should().Be(expectedYaml.Trim().Replace("\r\n", "\n"));
        result.UpdatedImageReferences.Count.Should().Be(2);
        result.UpdatedImageReferences.Should().Contain("nginx:1.25");
        result.UpdatedImageReferences.Should().Contain("busybox:stable");
    }

    [Test]
    public void UpdateImages_WithNestedContainerSpecs_UpdatesNestedImages()
    {
        const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: test-pod
spec:
  containers:
  - name: nginx
    image: nginx:1.21
  jobs:
    - spec:
        template:
          spec:
            containers:
            - name: worker
              image: my-registry.com/busybox:1.0
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().NotBeNull();
        result.UpdatedImageReferences.Count.Should().Be(2);
        result.UpdatedImageReferences.Should().Contain("nginx:1.25");
        result.UpdatedImageReferences.Should().Contain("busybox:stable");
    }

    [Test]
    public void UpdateImages_WithMultipleDocuments_UpdatesAllDocuments()
    {
        const string inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.21
---
apiVersion: v1
kind: Pod
metadata:
  name: busybox-pod
spec:
  containers:
  - name: busybox
    image: my-registry.com/busybox:1.0
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().NotBeNull();
        result.UpdatedImageReferences.Count.Should().Be(2);
        result.UpdatedImageReferences.Should().Contain("nginx:1.25");
        result.UpdatedImageReferences.Should().Contain("busybox:stable");
    }

    [Test]
    public void UpdateImages_WithNoMatchingImages_ReturnsNoChanges()
    {
        const string inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis-deployment
spec:
  template:
    spec:
      containers:
      - name: redis
        image: redis:6.0
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().Be(inputYaml);
        result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithEmptyContent_ReturnsNoChanges()
    {
        const string inputYaml = "";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().Be(inputYaml);
        result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithInvalidYaml_ReturnsNoChanges()
    {
        const string inputYaml = @"invalid: yaml: content: [unclosed";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().Be(inputYaml);
        result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithImageAlreadyUpToDate_ReturnsNoChanges()
    {
        const string inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-deployment
spec:
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25
";

        var imageReplacer = new StrategicMergePatchImageReplacer(inputYaml, ArgoCDConstants.DefaultContainerRegistry, log);

        var result = imageReplacer.UpdateImages(imagesToUpdate);

        result.UpdatedContents.Should().Be(inputYaml);
        result.UpdatedImageReferences.Should().BeEmpty();
    }
}