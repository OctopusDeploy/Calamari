using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    /// <summary>
    /// Integration tests for DirectoryUpdater.Process() workflow.
    /// Mirrors KustomizeUpdaterTests, focusing on non-default container registries.
    /// </summary>
    [TestFixture]
    public class DirectoryUpdaterTests
    {
        ILog log;
        ICalamariFileSystem fileSystem;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
            tempDir = fileSystem.CreateTemporaryDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            fileSystem?.DeleteDirectory(tempDir);
        }

        [Test]
        public void Process_WithImageOnNonDefaultRegistry_ProducesPatchSoChangesAreCommitted()
        {
            // Regression: an image on a non-default registry (e.g. GAR/GCR/ECR) must update the
            // manifest AND produce a JSON patch, so HasChanges() is true and the commit/push isn't
            // silently skipped.
            const string deployment = @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: helloworld
spec:
  template:
    spec:
      containers:
      - name: helloworld
        image: us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v1
";
            fileSystem.OverwriteFile(Path.Combine(tempDir, "deployment.yaml"), deployment);

            var garImages = new List<ContainerImageReferenceAndHelmReference>
            {
                new(ContainerImageReference.FromReferenceString(
                        "us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v2",
                        ArgoCDConstants.DefaultContainerRegistry)),
            };

            var sourceWithMetadata = new ApplicationSourceWithMetadata(
                new ApplicationSource { Path = "." },
                SourceType.Directory,
                0);

            var updater = new DirectoryUpdater(garImages, ArgoCDConstants.DefaultContainerRegistry, log, fileSystem);

            var result = updater.Process(sourceWithMetadata, tempDir);

            result.UpdatedImages.Should().NotBeEmpty();
            result.HasChanges().Should().BeTrue("a JSON patch must be produced so the commit/push is not skipped");
            result.PatchedFiles.Should().NotBeEmpty();

            var updatedContent = fileSystem.ReadFile(Path.Combine(tempDir, "deployment.yaml"));
            updatedContent.Should().Contain("us-docker.pkg.dev/shared-gke-dev-gqtrxy/argo-test/helloworld:v2");
            updatedContent.Should().NotContain("helloworld:v1");
        }
    }
}
