#if NET
using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions.UpdateArgoCdAppImages
{
  [TestFixture]
  public class ContainerImageReplacerTests
  {
    readonly string DefaultContainerRegistry = "docker.io";
    readonly List<ContainerImageReference> imagesToUpdate;

    public ContainerImageReplacerTests()
    {
      imagesToUpdate = new List<ContainerImageReference>
      {
        // We know this won't be null after parse
        ContainerImageReference.FromReferenceString("nginx:1.25"),
        ContainerImageReference.FromReferenceString("busybox:stable")
      };
    }

    [Test]
    public void UpdateImages_WithNoNewImages_ReturnsSameYaml()
    {
      const string inputYaml = @"
                                     kind: Deployment
                                     spec:
                                       template:
                                         spec:
                                           containers:
                                             - image: nginx:1.19
                                     ";

      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference>());

      result.UpdatedContents.Should().Be(inputYaml);
      result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithEmptyYaml_ReturnsEmpty()
    {
      var imageReplacer = new ContainerImageReplacer(string.Empty, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference>());

      result.UpdatedContents.Should().BeEmpty();
      result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithInvalidDocuments_IgnoresDocuments()
    {
      const string invalidYaml = @"
                                       this: is: not: valid: yaml
                                       - just:
                                         - random: stuff
                                       ";
      var imageReplacer = new ContainerImageReplacer(invalidYaml, DefaultContainerRegistry);
      var result = imageReplacer.UpdateImages(new List<ContainerImageReference>());

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(invalidYaml);
      result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithResourcesWithoutImages_LeavesYamlUnchanged()
    {
      const string yamlWithoutImages = @"
                                             kind: Service
                                             metadata:
                                               name: my-service
                                             spec:
                                               ports:
                                                 - port: 80
                                             ";

      var imageReplacer = new ContainerImageReplacer(yamlWithoutImages, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference> { ContainerImageReference.FromReferenceString("nginx:1.25") });

      result.UpdatedContents.Should().Be(yamlWithoutImages);
      result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithArgoAppManifest_LeavesYamlUnchanged()
    {
      const string argoApp = @"
                                   apiVersion: argoproj.io/v1alpha1
                                   kind: Application
                                   metadata:
                                     name: demo-containers-app
                                     namespace: argocd
                                     annotations:
                                       octopus.com/project: ""container-replacement-demo""
                                       octopus.com/environment: ""demo""
                                   spec:
                                     project: default
                                     source:
                                       repoURL: 'https://github.com/Jtango18/jt-argo-test.git'
                                       targetRevision: deploy-demo
                                       path: deploy-demo
                                     destination:
                                       server: 'https://kubernetes.default.svc'
                                       namespace: demo-app
                                     syncPolicy:
                                       automated:
                                         prune: true
                                         selfHeal: true
                                       syncOptions:
                                         - CreateNamespace=true
                                   ";

      var imageReplacer = new ContainerImageReplacer(argoApp, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference> { ContainerImageReference.FromReferenceString("nginx:1.25") });

      result.UpdatedContents.Should().Be(argoApp);
      result.UpdatedImageReferences.Should().BeEmpty();
    }

    [Test]
    public void UpdateImages_WithYamlComments_PreservesComments()
    {
      const string yamlWithComments = @"
                                            \# This is a comment
                                            kind: Deployment
                                            spec:
                                              template:
                                                spec:
                                                  containers:
                                                    - image: nginx:1.19 # Another comment
                                            ";
      var imageReplacer = new ContainerImageReplacer(yamlWithComments, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference> { ContainerImageReference.FromReferenceString("nginx:1.25") });

      result.UpdatedContents.Should().Be(yamlWithComments);
    }

    [Test]
    public void UpdateImages_WithQuotedReference_PreservesQuotes()
    {
      const string inputYaml = @"apiVersion: v1
kind: Pod
spec:
  containers:
    - name: my-container
      image: ""nginx:1.19""";
      const string expectedYaml = @"apiVersion: v1
kind: Pod
spec:
  containers:
    - name: my-container
      image: ""nginx:1.25""";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var updatedImage = new List<ContainerImageReference>
      {
        ContainerImageReference.FromReferenceString("nginx:1.25")
      };

      var result = imageReplacer.UpdateImages(updatedImage);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(1);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
    }

    [Test]
    public void DoesNotUpdateComments()
    {
      var commentLine = "#image: nginx:1.19 being used here";
      string inputYaml = @"apiVersion: v1
kind: Pod
spec:
  containers:
    - name: my-container
      "
                         + commentLine
                         + @"
      image: nginx:1.19";

      string expectedYaml = @"apiVersion: v1
kind: Pod
spec:
  containers:
    - name: my-container
      "
                            + commentLine
                            + @"
      image: nginx:1.25";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var updatedImage = new List<ContainerImageReference>
      {
        ContainerImageReference.FromReferenceString("nginx:1.25")
      };

      var result = imageReplacer.UpdateImages(updatedImage);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
    }

    [Test]
    public void UpdateImages_WithPodWithUpdates_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19 #Update
    - name: apline
      image: alpine:3.21 #Ignore
  initContainers:
    - name: init-busybox
      image: busybox:unstable #Update Init
      command: [""echo"", ""Init container added""]
";
      const string expectedYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.25 #Update
    - name: apline
      image: alpine:3.21 #Ignore
  initContainers:
    - name: init-busybox
      image: busybox:stable #Update Init
      command: [""echo"", ""Init container added""]
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_WithPodWithUpdatesForMultipleContainers_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19
    - name: init-busybox
      image: busybox:unstable
      command: [""echo"", ""Init container added""]
";
      const string expectedYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.25
    - name: init-busybox
      image: busybox:stable
      command: [""echo"", ""Init container added""]
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_WithPodWithUpdatesToMultipleInstancesOfSameImage_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19
    - name: more-nginx
      image: nginx:1.19
    - name: older-nginx
      image: nginx:1.12
";
      const string expectedYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.25
    - name: more-nginx
      image: nginx:1.25
    - name: older-nginx
      image: nginx:1.25
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var updatedImage = new List<ContainerImageReference>
      {
        ContainerImageReference.FromReferenceString("nginx:1.25"),
      };

      var result = imageReplacer.UpdateImages(updatedImage);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(1);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
    }

    // Tests for Kubernetes resources that have a spec.template.spec path to their containers
    [Theory]
    [TestCase("Deployment")]
    [TestCase("StatefulSet")]
    [TestCase("DaemonSet")]
    [TestCase("ReplicaSet")]
    [TestCase("Job", "batch/v1")]
    public void UpdateImages_SpecTemplateSpecPath_ReturnsUpdatedYaml(string kind, string api = "apps/v1")
    {
      var inputYaml = @$"
apiVersion: {api}
kind: {kind}
metadata:
  name: sample-{kind.ToLower()}
spec:
  replicas: 1
  selector:
    matchLabels:
      app: sample-{kind.ToLower()}
  template:
    metadata:
      labels:
        app: sample-{kind.ToLower()}
    spec:
      containers:
        - name: nginx
          image: nginx:1.19 #Update
        - name: apline
          image: alpine:3.21 #Ignore
      initContainers:
        - name: init-busybox
          image: busybox:unstable #Update Init
          command: [""echo"", ""Init container added""]
";
      var expectedYaml = @$"
apiVersion: {api}
kind: {kind}
metadata:
  name: sample-{kind.ToLower()}
spec:
  replicas: 1
  selector:
    matchLabels:
      app: sample-{kind.ToLower()}
  template:
    metadata:
      labels:
        app: sample-{kind.ToLower()}
    spec:
      containers:
        - name: nginx
          image: nginx:1.25 #Update
        - name: apline
          image: alpine:3.21 #Ignore
      initContainers:
        - name: init-busybox
          image: busybox:stable #Update Init
          command: [""echo"", ""Init container added""]
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_ForReplicationController_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: v1
kind: ReplicationController
metadata:
  name: sample-replictioncontroller
spec:
  replicas: 1
  selector:
    app: sample-replictioncontroller
  template:
    metadata:
      labels:
        app: sample-replictioncontroller
    spec:
      containers:
        - name: nginx
          image: nginx:1.19 #Update
        - name: alpine
          image: alpine:3.21 #Ignore
      initContainers:
        - name: init-busybox
          image: busybox:unstable #Update Init
          command: [""echo"", ""Init container added""]

";
      const string expectedYaml = @"
apiVersion: v1
kind: ReplicationController
metadata:
  name: sample-replictioncontroller
spec:
  replicas: 1
  selector:
    app: sample-replictioncontroller
  template:
    metadata:
      labels:
        app: sample-replictioncontroller
    spec:
      containers:
        - name: nginx
          image: nginx:1.25 #Update
        - name: alpine
          image: alpine:3.21 #Ignore
      initContainers:
        - name: init-busybox
          image: busybox:stable #Update Init
          command: [""echo"", ""Init container added""]

";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_TemplateSpecPath_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: v1
kind: PodTemplate
metadata:
  name: sample-podtemplate
template:
  metadata:
    labels:
      app: sample-podtemplate
  spec:
    containers:
      - name: nginx
        image: nginx:1.19 #Update
      - name: apline
        image: alpine:3.21 #Ignore
    initContainers:
      - name: init-busybox
        image: busybox:unstable #Update Init
        command: [""echo"", ""Init container added""]
";

      const string expectedYaml = @"
apiVersion: v1
kind: PodTemplate
metadata:
  name: sample-podtemplate
template:
  metadata:
    labels:
      app: sample-podtemplate
  spec:
    containers:
      - name: nginx
        image: nginx:1.25 #Update
      - name: apline
        image: alpine:3.21 #Ignore
    initContainers:
      - name: init-busybox
        image: busybox:stable #Update Init
        command: [""echo"", ""Init container added""]
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_WithCronJobResource_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: batch/v1
kind: CronJob
metadata:
  name: sample-cronjob
spec:
  schedule: ""*/5 * * * *""
  jobTemplate:
    spec:
      template:
        metadata:
          labels:
            app: sample-cronjob
        spec:
          containers:
            - name: nginx
              image: nginx:1.19 #Update
            - name: alpine
              image: alpine:3.21 #Ignore
          initContainers:
            - name: init-busybox
              image: busybox:unstable #Update Init
              command: [""echo"", ""Init container added""]
          restartPolicy: OnFailure
";
      const string expectedYaml = @"
apiVersion: batch/v1
kind: CronJob
metadata:
  name: sample-cronjob
spec:
  schedule: ""*/5 * * * *""
  jobTemplate:
    spec:
      template:
        metadata:
          labels:
            app: sample-cronjob
        spec:
          containers:
            - name: nginx
              image: nginx:1.25 #Update
            - name: alpine
              image: alpine:3.21 #Ignore
          initContainers:
            - name: init-busybox
              image: busybox:stable #Update Init
              command: [""echo"", ""Init container added""]
          restartPolicy: OnFailure
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_WithMultipleDocuments_CompletesSuccessfully()
    {
      const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19 #Update
---

apiVersion: v1
kind: Service
metadata:
  name: sample-service
spec:
  ports:
    - port: 80
";
      const string expectedYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.25 #Update
---

apiVersion: v1
kind: Service
metadata:
  name: sample-service
spec:
  ports:
    - port: 80
";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(imagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(1);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
    }

    [Test]
    public void UpdateImages_WithNoChangeToTag_ReturnsNoChanges()
    {
      const string inputYaml = @"
                                      apiVersion: v1
                                      kind: Pod
                                      metadata:
                                        name: sample-pod
                                      spec:
                                        containers:
                                          - name: nginx
                                            image: nginx:1.19
                                      ";
      var imageReplacer = new ContainerImageReplacer(inputYaml, DefaultContainerRegistry);

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference> { ContainerImageReference.FromReferenceString("nginx:1.19") });

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(inputYaml);

      result.UpdatedImageReferences.Count.Should().Be(0);
    }

    [Test]
    public void UpdateImages_WithPodUpdatesUsingCustomRegistry_ReturnsUpdatedYaml()
    {
      const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19 #Update
    - name: alpine
      image: alpine:3.21 #Ignore
  initContainers:
    - name: init-busybox
      image: busybox:unstable #Update Init
      command: [""echo"", ""Init container added""]
";
      const string expectedYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.25 #Update
    - name: alpine
      image: alpine:3.21 #Ignore
  initContainers:
    - name: init-busybox
      image: busybox:stable #Update Init
      command: [""echo"", ""Init container added""]
";

      List<ContainerImageReference> customRegistryImagesToUpdate = new List<ContainerImageReference>
      {
        // We know this won't be null after parse
        ContainerImageReference.FromReferenceString("my-custom.io/nginx:1.25"),
        ContainerImageReference.FromReferenceString("my-custom.io/busybox:stable")
      };

      var imageReplacer = new ContainerImageReplacer(inputYaml, "my-custom.io");

      var result = imageReplacer.UpdateImages(customRegistryImagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(2);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }

    [Test]
    public void UpdateImages_WithPodUpdatesUsingCustomRegistry_OnlyUpdatesCustomMatches()
    {
      const string inputYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19 #Ignore
    - name: alpine
      image: alpine:3.21 #Ignore
  initContainers:
    - name: init-busybox
      image: busybox:unstable #Update Init
      command: [""echo"", ""Init container added""]
";
      const string expectedYaml = @"
apiVersion: v1
kind: Pod
metadata:
  name: sample-pod
spec:
  containers:
    - name: nginx
      image: nginx:1.19 #Ignore
    - name: alpine
      image: alpine:3.21 #Ignore
  initContainers:
    - name: init-busybox
      image: busybox:stable #Update Init
      command: [""echo"", ""Init container added""]
";

      List<ContainerImageReference> customRegistryImagesToUpdate = new List<ContainerImageReference>
      {
        // We know this won't be null after parse
        ContainerImageReference.FromReferenceString("docker.io/nginx:1.25"), //This container should be ignored because it has a fully qualified registry that doesn't match the custom
        ContainerImageReference.FromReferenceString("my-custom.io/busybox:stable")
      };

      var imageReplacer = new ContainerImageReplacer(inputYaml, "my-custom.io");

      var result = imageReplacer.UpdateImages(customRegistryImagesToUpdate);

      result.UpdatedContents.Should().NotBeNull();
      result.UpdatedContents.Should().Be(expectedYaml);
      result.UpdatedImageReferences.Count.Should().Be(1);
      result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
    }


    [Test]
    public void WhyDoesntGuestBookwork()
    {
      var inputYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: guestbook-ui
spec:
  replicas: 1
  revisionHistoryLimit: 3
  selector:
    matchLabels:
      app: guestbook-ui
  template:
    metadata:
      labels:
        app: guestbook-ui
    spec:
      containers:
      - image: quay.io/argoprojlabs/argocd-e2e-container:0.1
        name: guestbook-ui
        ports:
        - containerPort: 80";

      var expectedOutput = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: guestbook-ui
spec:
  replicas: 1
  revisionHistoryLimit: 3
  selector:
    matchLabels:
      app: guestbook-ui
  template:
    metadata:
      labels:
        app: guestbook-ui
    spec:
      containers:
      - image: quay.io/argoprojlabs/argocd-e2e-container:0.3
        name: guestbook-ui
        ports:
        - containerPort: 80";

      var imageReplacer = new ContainerImageReplacer(inputYaml, "my-custom.io");

      var result = imageReplacer.UpdateImages(new List<ContainerImageReference> { ContainerImageReference.FromReferenceString("quay.io/argoprojlabs/argocd-e2e-container:0.3") });
      result.UpdatedContents.Should().Be(expectedOutput);
    }
  }
}
#endif
