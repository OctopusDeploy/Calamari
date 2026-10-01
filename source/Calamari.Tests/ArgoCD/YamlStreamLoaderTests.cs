using Calamari.ArgoCD;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class YamlStreamLoaderTests
    {
        [Test]
        public void SerializeDocuments_WithCrlfOriginal_EmitsCrlfOnEveryLine()
        {
            const string original = "kind: Kustomization\r\nnamespace: dev\r\n";

            var result = Serialize(original);

            result.Should().Be("kind: Kustomization\r\nnamespace: dev\r\n");
        }

        [Test]
        public void SerializeDocuments_WithLfOriginal_EmitsLfOnEveryLine()
        {
            const string original = "kind: Kustomization\nnamespace: dev\n";

            var result = Serialize(original);

            result.Should().Be("kind: Kustomization\nnamespace: dev\n");
        }

        [Test]
        public void SerializeDocuments_WithOriginalMissingTrailingNewline_DoesNotAddOne()
        {
            const string original = "kind: Kustomization\nnamespace: dev";

            var result = Serialize(original);

            result.Should().Be("kind: Kustomization\nnamespace: dev");
        }

        [Test]
        public void SerializeDocuments_WithMultipleDocuments_SeparatesUsingTheOriginalLineEnding()
        {
            const string original = "kind: First\r\n---\r\nkind: Second\r\n";

            var result = Serialize(original);

            result.Should().Be("kind: First\r\n---\r\nkind: Second\r\n");
        }

        static string Serialize(string original)
        {
            var stream = YamlStreamLoader.TryLoadSilent(original);
            return YamlStreamLoader.SerializeDocuments(stream!.Documents, original);
        }
    }
}
