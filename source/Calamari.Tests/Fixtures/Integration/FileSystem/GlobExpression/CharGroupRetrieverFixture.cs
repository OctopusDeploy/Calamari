using System;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.Fixtures.Integration.FileSystem.GlobExpression
{
    [TestFixture]
    public class CharGroupRetrieverFixture
    {
        [Test]
        // Returns no groups
        [TestCase("hello.txt", "")] // No {} groups
        [TestCase("Here/[hello.txt", "")] // dangling [
        [TestCase("Her[e/h]ello.txt", "")] // [] separated by directory separator
        [TestCase("Here/[te{s]t,mp}.txt", "")] // [] intersects with {}

        // Returns groups
        [TestCase("Here/[hi].txt", "{startIndex:5,length:4,options:['h','i','[hi]']}")] // Basic char group, will also return literal expression.
        [TestCase("Here/[a-c].txt", "{startIndex:5,length:5,options:['a','b','c']}")] // Range char Group
        [TestCase("This/I[st]/Fi[rn]e.png",
            "{startIndex:6,length:4,options:['s','t','[st]']};" +
            "{startIndex:13,length:4,options:['r','n','[rn]']}")] // Two char groups
        [TestCase("Here/[?*.].txt", "{startIndex:5,length:5,options:['?','*','.','[?*.]']}")] // Other characters
        public void GetCharGroups_ForGivenPath_ReturnsExpectedGroups(string path, string groupJsonStrings)
        {
            var expectedGroups = groupJsonStrings.IsNullOrEmpty()
                ? Enumerable.Empty<Group>()
                : groupJsonStrings.Split(';').Select(JsonConvert.DeserializeObject<Group>);

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