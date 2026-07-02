using Calamari.ArgoCD.Conventions.UpdateImageTag;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.Tests.ArgoCD.Conventions
{
    [TestFixture]
    public class FileUpdateResultTests
    {
        [Test]
        public void Merge_CombinesImagesReplacedPatchedAndRemovedFiles()
        {
            var first = new FileUpdateResult(["nginx:1.27.1"], [new FileHash("a.yaml", "h1")], [new FileJsonPatch("a.yaml", "patchA")], ["removedA.yaml"]);
            var second = new FileUpdateResult(["redis:7.0", "nginx:1.27.1"], [new FileHash("b.yaml", "h2")], [new FileJsonPatch("b.yaml", "patchB")], ["removedB.yaml"]);

            var merged = FileUpdateResult.Merge([first, second]);

            merged.UpdatedImages.Should().BeEquivalentTo(["nginx:1.27.1", "redis:7.0"], "duplicate images are de-duplicated");
            merged.ReplacedFiles.Should().BeEquivalentTo([new FileHash("a.yaml", "h1"), new FileHash("b.yaml", "h2")]);
            merged.PatchedFiles.Should().BeEquivalentTo([new FileJsonPatch("a.yaml", "patchA"), new FileJsonPatch("b.yaml", "patchB")]);
            merged.FilesRemoved.Should().BeEquivalentTo(["removedA.yaml", "removedB.yaml"]);
            merged.HasChanges().Should().BeTrue();
        }

        [Test]
        public void Merge_OfEmptyResults_HasNoChanges()
        {
            var merged = FileUpdateResult.Merge([FileUpdateResult.EmptyFileUpdateResult, FileUpdateResult.EmptyFileUpdateResult]);

            merged.HasChanges().Should().BeFalse();
            merged.UpdatedImages.Should().BeEmpty();
            merged.ReplacedFiles.Should().BeEmpty();
            merged.PatchedFiles.Should().BeEmpty();
            merged.FilesRemoved.Should().BeEmpty();
        }
    }
}
