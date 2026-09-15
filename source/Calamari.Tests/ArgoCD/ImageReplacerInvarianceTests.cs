using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    /// <summary>
    /// Systematically varies the formatting a customer's file might use and asserts the only thing a
    /// replacer may ever do is replace the image reference. Anything else — reformatting, a lost
    /// anchor, a duplicated or truncated fragment — fails here, whatever the cause.
    /// </summary>
    [TestFixture]
    public class ImageReplacerInvarianceTests
    {
        const string OldImage = "nginx:1.21";
        readonly ILog log = new InMemoryLog();

        [TestCaseSource(nameof(KustomizationVariants))]
        public void InlineJsonPatch_OnlyEverReplacesTheImageReference(Variant variant)
        {
            AssertOnlyTheImageMayChange(variant,
                                        (content, images) => new InlineJsonPatchReplacer(content, ArgoCDConstants.DefaultContainerRegistry, log).UpdateImages(images));
        }

        [TestCaseSource(nameof(StrategicMergeVariants))]
        public void InlineStrategicMerge_OnlyEverReplacesTheImageReference(Variant variant)
        {
            AssertOnlyTheImageMayChange(variant,
                                        (content, images) => new InlineStrategicMergeImageReplacer(content, ArgoCDConstants.DefaultContainerRegistry, log).UpdateImages(images));
        }

        [TestCaseSource(nameof(Json6902Variants))]
        public void Json6902Patch_OnlyEverReplacesTheImageReference(Variant variant)
        {
            AssertOnlyTheImageMayChange(variant,
                                        (content, images) => new YamlJson6902PatchImageReplacer(content, ArgoCDConstants.DefaultContainerRegistry, log).UpdateImages(images));
        }

        [TestCaseSource(nameof(KustomizationVariants))]
        public void InlineJsonPatch_WithNoMatchingImage_ReturnsTheFileUnchanged(Variant variant)
        {
            var noMatch = Images("redis:7.0");

            var result = new InlineJsonPatchReplacer(variant.Content, ArgoCDConstants.DefaultContainerRegistry, log).UpdateImages(noMatch);

            result.UpdatedContents.Should().Be(variant.Content);
            result.UpdatedImageReferences.Should().BeEmpty();
        }

        void AssertOnlyTheImageMayChange(Variant variant, System.Func<string, IReadOnlyCollection<ContainerImageReferenceAndHelmReference>, ImageReplacementResult> replace)
        {
            YamlStreamLoader.TryLoadSilent(variant.Content).Should().NotBeNull($"the generated variant {variant.Name} should be valid YAML");

            foreach (var newTag in new[] { "nginx:9", "nginx:1.25", "nginx:1.25-alpine-with-a-long-suffix" })
            {
                var result = replace(variant.Content, Images(newTag));

                var unchanged = variant.Content;
                var surgicallyChanged = variant.Content.Replace(OldImage, newTag);

                if (variant.ExpectUpdate)
                {
                    // Asserting the exact result, not "unchanged or changed", so that a regression to
                    // silently declining every update is caught rather than passing as a safe no-op.
                    result.UpdatedContents.Should()
                          .Be(surgicallyChanged, $"{variant.Name} with {newTag} must change only the image reference");
                }
                else
                {
                    result.UpdatedContents.Should()
                          .BeOneOf(new[] { unchanged, surgicallyChanged },
                                   $"{variant.Name} with {newTag} must either decline to change the file or change only the image reference");
                }

                YamlStreamLoader.TryLoadSilent(result.UpdatedContents)
                                .Should()
                                .NotBeNull($"{variant.Name} with {newTag} must still be valid YAML");
            }
        }

        static List<ContainerImageReferenceAndHelmReference> Images(string image)
        {
            return new List<ContainerImageReferenceAndHelmReference>
            {
                new(ContainerImageReference.FromReferenceString(image, ArgoCDConstants.DefaultContainerRegistry))
            };
        }

        public record Variant(string Name, string Content, bool ExpectUpdate)
        {
            public override string ToString() => Name;
        }

        static IEnumerable<Variant> KustomizationVariants()
        {
            foreach (var shape in Shapes())
            {
                var body = new List<string>
                {
                    "apiVersion: kustomize.config.k8s.io/v1beta1",
                    "kind: Kustomization",
                    "patches:",
                    "  - target:",
                    $"      kind: Deployment{shape.InlineComment}",
                    $"    patch: {shape.BlockIndicator}",
                };
                body.AddRange(PatchLines(shape));

                yield return Build($"kustomization[{shape.Name}]", shape, body);
            }
        }

        static IEnumerable<Variant> StrategicMergeVariants()
        {
            foreach (var shape in Shapes())
            {
                var body = new List<string>
                {
                    "apiVersion: kustomize.config.k8s.io/v1beta1",
                    "kind: Kustomization",
                    "patchesStrategicMerge:",
                    $"  - {shape.BlockIndicator}",
                };
                body.AddRange(PatchLines(shape, blockIndent: "    "));

                yield return Build($"strategicMerge[{shape.Name}]", shape, body);
            }
        }

        static IEnumerable<Variant> Json6902Variants()
        {
            foreach (var quote in new[] { "", "\"", "'" })
            foreach (var anchor in new[] { "", "&img " })
            foreach (var newLine in new[] { "\n", "\r\n" })
            foreach (var trailing in new[] { true, false })
            {
                var name = $"6902[quote={(quote == "" ? "none" : quote)},anchor={(anchor == "" ? "no" : "yes")},nl={(newLine == "\n" ? "LF" : "CRLF")},trailingNl={trailing}]";
                var body = new List<string>
                {
                    "# rollout patch",
                    "- op: replace",
                    "  path: /spec/template/spec/containers/0/image",
                    $"  value: {anchor}{quote}{OldImage}{quote}",
                };

                var content = string.Join(newLine, body) + (trailing ? newLine : "");
                yield return new Variant(name, content, ExpectUpdate: true);
            }

            foreach (var aliased in AliasSharingVariants())
                yield return aliased;
        }

        /// <summary>
        /// An alias resolves to the same node object, so the same scalar gets visited — and edited —
        /// more than once. Equal-length tags can hide a splice applied at stale offsets, so the
        /// invariance assertions deliberately try tags shorter and longer than the original.
        /// </summary>
        static IEnumerable<Variant> AliasSharingVariants()
        {
            foreach (var newLine in new[] { "\n", "\r\n" })
            foreach (var quote in new[] { "", "\"" })
            foreach (var trailing in new[] { true, false })
            {
                var name = $"6902-alias[nl={(newLine == "\n" ? "LF" : "CRLF")},quote={(quote == "" ? "none" : quote)},trailingNl={trailing}]";
                var body = new List<string>
                {
                    "- op: add",
                    "  path: /spec/template/spec/containers",
                    "  value:",
                    "  - &web",
                    "    name: nginx",
                    $"    image: {quote}{OldImage}{quote}",
                    "- op: add",
                    "  path: /spec/template/spec/initContainers",
                    "  value:",
                    "  - *web",
                };

                var content = string.Join(newLine, body) + (trailing ? newLine : "");
                yield return new Variant(name, content, ExpectUpdate: true);
            }
        }

        static IEnumerable<string> PatchLines(Shape shape, string blockIndent = "      ")
        {
            yield return $"{blockIndent}apiVersion: apps/v1";
            yield return $"{blockIndent}kind: Deployment";
            if (shape.BlankLineInBlock)
                yield return "";
            yield return $"{blockIndent}spec:";
            yield return $"{blockIndent}  template:";
            yield return $"{blockIndent}    spec:";
            yield return $"{blockIndent}      containers:";
            yield return $"{blockIndent}        - name: nginx{shape.InlineComment}";
            yield return $"{blockIndent}          image: {shape.Quote}{OldImage}{shape.Quote}";
        }

        static Variant Build(string name, Shape shape, List<string> body)
        {
            var lines = new List<string>();
            if (shape.LeadingComment)
                lines.Add("# managed by the platform team");
            lines.AddRange(body);

            var content = string.Join(shape.NewLine, lines) + (shape.TrailingNewLine ? shape.NewLine : "");
            return new Variant($"{name}", content, ExpectUpdate: true);
        }

        record Shape(string Name, string NewLine, bool TrailingNewLine, bool LeadingComment, string InlineComment, bool BlankLineInBlock, string BlockIndicator, string Quote);

        static IEnumerable<Shape> Shapes()
        {
            foreach (var newLine in new[] { "\n", "\r\n" })
            foreach (var trailing in new[] { true, false })
            foreach (var leadingComment in new[] { true, false })
            foreach (var inlineComment in new[] { "", "   # the web tier" })
            foreach (var blankInBlock in new[] { true, false })
            foreach (var indicator in new[] { "|", "|-" })
            foreach (var quote in new[] { "", "\"", "'" })
            {
                var name = string.Join(",",
                                       newLine == "\n" ? "LF" : "CRLF",
                                       $"trailingNl={trailing}",
                                       $"leadComment={leadingComment}",
                                       $"inlineComment={inlineComment != ""}",
                                       $"blankInBlock={blankInBlock}",
                                       $"block={indicator}",
                                       $"quote={(quote == "" ? "none" : quote)}");

                yield return new Shape(name, newLine, trailing, leadingComment, inlineComment, blankInBlock, indicator, quote);
            }
        }
    }
}
