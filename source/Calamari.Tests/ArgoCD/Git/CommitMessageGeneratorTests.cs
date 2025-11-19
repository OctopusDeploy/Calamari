using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Git;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git
{
    [TestFixture]
    public class CommitMessageGeneratorTests
    {
        readonly CommitMessageGenerator commitMessageGenerator = new CommitMessageGenerator();

        [Test]
        public void SummaryWithNoImages_SummaryAndNoImage()
        {
            var result = commitMessageGenerator.GenerateDescription(new HashSet<string>(), "Foo");

            var expected = @"Foo

---
No images updated".ReplaceLineEndings("\n");
            result.Should().Be(expected);
        }

        [Test]
        public void SummaryWithOneImage_SummaryAndImage()
        {
            var result = commitMessageGenerator.GenerateDescription( new HashSet<string>() { "nginx" }, "Foo");

            var expected = @"Foo

---
Images updated:
- nginx".ReplaceLineEndings("\n");
            result.Should().Be(expected);
        }

        [Test]
        public void SummaryWithThreeImages_SummaryAndImagesSorted()
        {
            var result = commitMessageGenerator.GenerateDescription(new HashSet<string>() {"nginx", "alpine", "ubuntu"}, "Foo");

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
            var result = commitMessageGenerator.GenerateDescription( new HashSet<string>() {"nginx"},description);

            var expected = @"Dolores animi quia quae enim hic.

Quibusdam qui maxime eos et magnam quod minus rerum perferendis eum iusto neque et tenetur. Porro illum praesentium sit dolorem rerum accusantium enim repellendus qui iste.

---
Images updated:
- nginx".ReplaceLineEndings("\n");
            result.Should().Be(expected.ReplaceLineEndings("\n"));
        }
    }
}
