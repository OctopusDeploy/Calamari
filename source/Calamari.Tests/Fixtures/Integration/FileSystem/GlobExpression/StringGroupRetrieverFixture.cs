using System;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem.GlobExpression
{
    [TestFixture]
    public class StringGroupRetrieverFixture
    {
        private static object[] _getStringGroupsCases = {
            // Returns no groups
            new object[] { "hello.txt", Array.Empty<Group>() }, // No {} groups
            new object[] { "Here/{hello.txt", Array.Empty<Group>() }, // Single dangling {
            new object[] { "Her{e/h,e}llo.txt", Array.Empty<Group>() }, // {} separated by directory separator
            new object[] { "Dir/{Here}/hello.txt", Array.Empty<Group>() }, // {} with no comma between
            new object[] { "Dir/{Her[e,orHere}/he]llo.txt", Array.Empty<Group>() }, // {} intersects with []

            // Returns groups
            new object[] { "Dir/{Here,orHere}/hello.txt", new[] { new Group(4, 13, new[] { "Here", "orHere" }) } }, // Folder
            new object[] { "Dir/Here/{hello,goodbye}.txt", new[] { new Group(9, 15, new[] { "hello", "goodbye" }) } }, // File
            new object[] { "Dir/Here/{hello,goodbye,salut}.txt", new[] { new Group(9, 21, new[] { "hello", "goodbye", "salut" })} }, // Three options
            new object[]
            {
                "Dir/{Here,orHere}/{hello,goodbye}.txt",
                new[]
                {
                    new Group(4, 13, new[] { "Here", "orHere" }),
                    new Group(18, 15, new[] { "hello", "goodbye" })
                }
            }, // Both Folder and File
            new object[] { "Dir/{H*e$re,orH|er?e}/hello.txt", new[] { new Group(4, 17, new[] { "H*e$re", "orH|er?e" }) } }, // With other characters
            new object[] { "Dir/{he{r,e}or,Here}/hello.txt", new[] { new Group(7, 5, new[] { "r", "e" }) } } // Outer {} are ignored.
        };

        [Test]
        [TestCaseSource(nameof(_getStringGroupsCases))]
        public void GetStringGroups_ForGivenPath_ReturnsExpectedGroups(string path, Group[] expectedGroups)
        {
            var resultGroups = StringGroupRetriever.GetStringGroups(path);

            resultGroups.Should().BeEquivalentTo(expectedGroups);
        }
    }
}