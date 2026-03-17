using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class JsonPatchImageReplacerTests
    {
        readonly List<ContainerImageReferenceAndHelmReference> imagesToUpdate = new()
        {
            new(ContainerImageReference.FromReferenceString("nginx:1.25", ArgoCDConstants.DefaultContainerRegistry)),
            new(ContainerImageReference.FromReferenceString("busybox:stable", "my-registry.com")),
        };

        ILog log = new InMemoryLog();

        [Test]
        public void UpdateImages_WithSimpleReplaceOperation_UpdatesImageReference()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.21""
  }
]";

            const string expectedJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.25""
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedContents.Should().Be(expectedJson);
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithAddOperation_UpdatesImageReference()
        {
            const string inputJson = @"[
  {
    ""op"": ""add"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""my-registry.com/busybox:1.0""
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "busybox:stable");
            result.UpdatedContents.Should().Contain("my-registry.com/busybox:stable");
        }

        [Test]
        public void UpdateImages_WithObjectValue_UpdatesNestedImageReferences()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec"",
    ""value"": {
      ""containers"": [
        {
          ""name"": ""nginx"",
          ""image"": ""nginx:1.21""
        },
        {
          ""name"": ""busybox"",
          ""image"": ""my-registry.com/busybox:1.0""
        }
      ]
    }
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
            result.UpdatedContents.Should().Contain("nginx:1.25");
            result.UpdatedContents.Should().Contain("my-registry.com/busybox:stable");
        }

        [Test]
        public void UpdateImages_WithArrayValue_UpdatesImageReferencesInArray()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers"",
    ""value"": [
      {
        ""name"": ""nginx"",
        ""image"": ""nginx:1.21""
      },
      {
        ""name"": ""sidecar"",
        ""image"": ""redis:6.0""
      }
    ]
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
            result.UpdatedContents.Should().Contain("nginx:1.25");
        }

        [Test]
        public void UpdateImages_WithComplexNestedStructure_UpdatesAllMatchingImages()
        {
            const string inputJson = @"[
  {
    ""op"": ""add"",
    ""path"": ""/spec/jobTemplate/spec/template/spec"",
    ""value"": {
      ""initContainers"": [
        {
          ""name"": ""init"",
          ""image"": ""my-registry.com/busybox:1.0""
        }
      ],
      ""containers"": [
        {
          ""name"": ""main"",
          ""image"": ""nginx:1.21""
        }
      ]
    }
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
        }

        [Test]
        public void UpdateImages_WithMultiplePatchOperations_UpdatesAllMatchingImages()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.21""
  },
  {
    ""op"": ""add"",
    ""path"": ""/spec/template/spec/initContainers"",
    ""value"": [
      {
        ""name"": ""init"",
        ""image"": ""my-registry.com/busybox:1.0""
      }
    ]
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(2);
            result.UpdatedImageReferences.Should().Contain("nginx:1.25");
            result.UpdatedImageReferences.Should().Contain("busybox:stable");
        }

        [Test]
        public void UpdateImages_WithNoMatchingImages_ReturnsNoChanges()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""redis:6.0""
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputJson);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithImageAlreadyUpToDate_ReturnsNoChanges()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.25""
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputJson);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithEmptyContent_ReturnsNoChanges()
        {
            const string inputJson = "";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputJson);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithInvalidJson_ReturnsNoChanges()
        {
            const string inputJson = @"{ invalid json [";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputJson);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithNonArrayJson_ReturnsNoChanges()
        {
            const string inputJson = @"{
  ""op"": ""replace"",
  ""path"": ""/spec/template/spec/containers/0/image"",
  ""value"": ""nginx:1.21""
}";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().Be(inputJson);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        [Test]
        public void UpdateImages_WithNonImageStringValues_IgnoresNonImageStrings()
        {
            const string inputJson = @"[
  {
    ""op"": ""replace"",
    ""path"": ""/metadata/name"",
    ""value"": ""my-deployment""
  },
  {
    ""op"": ""replace"",
    ""path"": ""/spec/template/spec/containers/0/image"",
    ""value"": ""nginx:1.21""
  }
]";

            var imageReplacer = new JsonPatchImageReplacer(inputJson, ArgoCDConstants.DefaultContainerRegistry, log);

            var result = imageReplacer.UpdateImages(imagesToUpdate);

            result.UpdatedContents.Should().NotBeNull();
            result.UpdatedImageReferences.Count.Should().Be(1);
            result.UpdatedImageReferences.Should().ContainSingle(r => r == "nginx:1.25");
            result.UpdatedContents.Should().Contain("my-deployment"); // Non-image values should remain unchanged
        }
    }
}