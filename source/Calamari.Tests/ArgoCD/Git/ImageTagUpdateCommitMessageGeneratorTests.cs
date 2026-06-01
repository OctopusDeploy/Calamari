#nullable enable
using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Git;
using FluentAssertions;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git
{
    [TestFixture]
    public class ImageTagUpdateCommitMessageGeneratorTests
    {
        [Test]
        public void SummaryWithNoImages_SummaryAndNoImage()
        {
            var imageTagUpdateCommitMessageGenerator = new ImageTagUpdateCommitMessageGenerator("Foo");
            var result = imageTagUpdateCommitMessageGenerator.GenerateDescription(CreateFileUpdateResult(new HashSet<string>()));
            var expected = @"Foo

---
No images updated".ReplaceLineEndings("\n");
            result.Should().Be(expected);
        }

        [Test]
        public void SummaryWithOneImage_SummaryAndImage()
        {
            var imageTagUpdateCommitMessageGenerator = new ImageTagUpdateCommitMessageGenerator("Foo");
            var result = imageTagUpdateCommitMessageGenerator.GenerateDescription( CreateFileUpdateResult(new HashSet<string>() { "nginx" }));

            var expected = @"Foo

---
Images updated:
- nginx".ReplaceLineEndings("\n");
            result.Should().Be(expected);
        }

        [Test]
        public void SummaryWithThreeImages_SummaryAndImagesSorted()
        {
            var imageTagUpdateCommitMessageGenerator = new ImageTagUpdateCommitMessageGenerator("Foo");
            var result = imageTagUpdateCommitMessageGenerator.GenerateDescription(CreateFileUpdateResult(new HashSet<string>() {"nginx", "alpine", "ubuntu"}));

            var expected = @"Foo

---
Images updated:
- alpine
- nginx
- ubuntu".ReplaceLineEndings("\n");
            result.Should().Be(expected);
        }
        
        [Test]
        public void SummaryAndDescriptionWithOneImage_SummaryAndDescriptionAndImage()
        {
            var description = @"Dolores animi quia quae enim hic.

Quibusdam qui maxime eos et magnam quod minus rerum perferendis eum iusto neque et tenetur. Porro illum praesentium sit dolorem rerum accusantium enim repellendus qui iste.".ReplaceLineEndings("\n");
            var imageTagUpdateCommitMessageGenerator = new ImageTagUpdateCommitMessageGenerator(description);
            var result = imageTagUpdateCommitMessageGenerator.GenerateDescription( CreateFileUpdateResult(new HashSet<string>() {"nginx"}));

            var expected = @"Dolores animi quia quae enim hic.

Quibusdam qui maxime eos et magnam quod minus rerum perferendis eum iusto neque et tenetur. Porro illum praesentium sit dolorem rerum accusantium enim repellendus qui iste.

---
Images updated:
- nginx".ReplaceLineEndings("\n");
            result.Should().Be(expected.ReplaceLineEndings("\n"));
        }
        
        FileUpdateResult CreateFileUpdateResult(HashSet<string> imagesUpdated)
        {
            return new FileUpdateResult(imagesUpdated, [], [], []);
        }
    }
}
