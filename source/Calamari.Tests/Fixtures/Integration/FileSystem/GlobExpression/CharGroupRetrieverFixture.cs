using System;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem.GlobExpression
{
    [TestFixture]
    public class CharGroupRetrieverFixture
    {
        private static object[] _getCharGroupsCases = {
            // Returns no groups
            new object[] { "hello.txt", Array.Empty<Group>() }, // No [] groups
            new object[] { "Here/[hello.txt", Array.Empty<Group>() }, // Single dangling [
            new object[] { "Her[e/h]ello.txt", Array.Empty<Group>() }, // [] separated by directory separator
            new object[] { "Here/[te{s]t,mp}.txt", Array.Empty<Group>() }, // [] intersects with {}

            // Returns groups
            new object[] { "Here/[hi].txt", new[] { new Group(5, 4, new[] { "h", "i" , "[hi]"}) } }, // Basic char group, will also return literal expression.
            new object[] { "Here/[a-c].txt", new[] { new Group(5, 5, new[] { "a", "b", "c"}) } }, // Range char Group
            new object[] { "This/I[st]/Fi[rn]e.png",
                new[]
                {
                    new Group(6, 4, new[] { "s", "t", "[st]" }),
                    new Group(13, 4, new[] { "r", "n", "[rn]" })
                }
            }, // Two char groups
            new object[] { "Here/[?*.].txt", new[] { new Group(5, 5, new[] { "?", "*", ".", "[?*.]" })} } // Other characters
        };


        [Test]
        [TestCaseSource(nameof(_getCharGroupsCases))]
        public void GetCharGroups_ForGivenPath_ReturnsExpectedGroups(string path, Group[] expectedGroups)
        {
            var resultGroups = CharGroupRetriever.GetCharGroups(path);

            resultGroups.Should().BeEquivalentTo(expectedGroups);
        }

        [Test]
        [TestCase("Here/[c-a].txt")]
        [TestCase("Here/[ab-c].txt")]
        [TestCase("Here/[ac-].txt")]
        [TestCase("Here/[-ac].txt")]
        [TestCase("Here/[-ac].txt")]
        [TestCase("Here/[a-cd].txt")]
        [TestCase("Here/[a-a].txt")]
        public void GetCharGroups_ThrowsException_ForInvalidCharRange(string path)
        {
            Action act = () => CharGroupRetriever.GetCharGroups(path);

            act.Should().Throw<InvalidOperationException>();
        }
    }
}