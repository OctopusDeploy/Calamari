using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.Kubernetes.Patching.JsonPatch;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Patching.JsonPatch;

[TestFixture]
public class JsonPointerExtensionMethodsTests
{
    // see 5. JSON String Representation in https://www.rfc-editor.org/rfc/rfc6901
    [Test]
    public void NavigateTests_RFC6901()
    {
        var doc = JsonNode.Parse(
            """
            {
               "foo": ["bar", "baz"],
               "": 0,
               "a/b": 1,
               "c%d": 2,
               "e^f": 3,
               "g|h": 4,
               "i\\j": 5,
               "k\"l": 6,
               " ": 7,
               "m~n": 8
            }
            """);

        using var scope = new AssertionScope();

        doc.NavigateToNode(new JsonPointer("")).Should().Be(doc);

        JsonAssert.Equal(
            JsonNode.Parse("""["bar", "baz"]"""),
            doc.NavigateToNode(new JsonPointer("/foo")));

        JsonAssert.Equal(
            (JsonNode)"bar",
            doc.NavigateToNode(new JsonPointer("/foo/0")));

        JsonAssert.Equal(
            (JsonNode)0,
            doc.NavigateToNode(new JsonPointer("/")));

        JsonAssert.Equal(
            (JsonNode)1,
            doc.NavigateToNode(new JsonPointer("/a~1b")));

        JsonAssert.Equal(
            (JsonNode)2,
            doc.NavigateToNode(new JsonPointer("/c%d")));

        JsonAssert.Equal(
            (JsonNode)3,
            doc.NavigateToNode(new JsonPointer("/e^f")));

        JsonAssert.Equal(
            (JsonNode)4,
            doc.NavigateToNode(new JsonPointer("/g|h")));

        JsonAssert.Equal(
            (JsonNode)5,
            doc.NavigateToNode(new JsonPointer("""/i\j""")));

        JsonAssert.Equal(
            (JsonNode)6,
            doc.NavigateToNode(new JsonPointer("""/k"l""")));

        JsonAssert.Equal(
            (JsonNode)7,
            doc.NavigateToNode(new JsonPointer("/ ")));

        JsonAssert.Equal(
            (JsonNode)8,
            doc.NavigateToNode(new JsonPointer("/m~0n")));
    }

    [Test]
    public void NavigateTests_Nulls()
    {
        using var scope = new AssertionScope();

        JsonNode.Parse("""{ "foo": null }""")
            .NavigateToNode(new JsonPointer("/foo"))
            .Should()
            .BeNull("null literal found for property foo");

        JsonNode.Parse("""{ "foo": { "bar": null } }""")
            .NavigateToNode(new JsonPointer("/foo/bar"))
            .Should()
            .BeNull("null literal found for property bar under foo container");

        JsonNode.Parse("""["x", 2, null, "y"]""")
            .NavigateToNode(new JsonPointer("/2"))
            .Should()
            .BeNull("null literal found at array index 2");

        JsonNode.Parse("""{ "foo": ["x", 2, null, "y"] }""")
            .NavigateToNode(new JsonPointer("/foo/2"))
            .Should()
            .BeNull("null literal found at array index 2 under foo container");

        var act1 = () => JsonNode.Parse("""{ "foo": null }""").NavigateToNode(new JsonPointer("/foo/bar"));
        act1.Should().Throw<JsonException>().WithMessage("Attempt to navigate to /foo/bar, but found null at property 'foo'");

        var act2 = () => JsonNode.Parse("""["x", 2, null, "y"]""").NavigateToNode(new JsonPointer("/2/foo"));
        act2.Should().Throw<JsonException>().WithMessage("Attempt to navigate to /2/foo, but found null at array index '2'");
    }

    [Test]
    public void IsDescendantOf()
    {
        // Rules:
        // Anything is a descendant of "" (empty string refers to the whole document)
        //   /foo/bar is a descendant of /foo
        //   /items/0/name is a descendant of /items
        //   /foo is not descendant of /foo/bar
        //   /dog is not descendant of /cat
        //   /foo is a not descendant of /foo (itself)

        using var scope = new AssertionScope();

        PositiveTest("/foo/bar", "");
        PositiveTest("/", "");
        NegativeTest("", "");

        PositiveTest("/foo/bar", "/foo");
        PositiveTest("/items/0/name", "/items");
        NegativeTest("/foo", "/foo/bar");
        NegativeTest("/dog", "/cat");
        NegativeTest("/foo", "/foo");

        static void PositiveTest(string candidate, string potentialAncestor)
            => new JsonPointer(candidate)
                .IsDescendantOf(new JsonPointer(potentialAncestor))
                .Should()
                .BeTrue($"\"{candidate}\" should be a descendant of \"{potentialAncestor}\"");

        static void NegativeTest(string candidate, string potentialAncestor)
            => new JsonPointer(candidate)
                .IsDescendantOf(new JsonPointer(potentialAncestor))
                .Should()
                .BeFalse($"\"{candidate}\" should not be a descendant of \"{potentialAncestor}\"");
    }
}
