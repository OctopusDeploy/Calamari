using System.Linq;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.Fixtures.Integration.FileSystem.GlobExpression
{
    [TestFixture]
    public class StringGroupRetrieverFixture
    {
        [Test]
        // Returns no groups
        [TestCase("hello.txt", "")] // No {} groups
        [TestCase("Here/{hello.txt", "")] // Single dangling {
        [TestCase("Her{e/h,e}llo.txt", "")] // {} separated by directory separator
        [TestCase("Dir/{Here}/hello.txt", "")] // {} with no comma between
        [TestCase("Dir/{Her[e,orHere}/he]llo.txt", "")] // {} intersects with []

        // Returns groups
        [TestCase("Dir/{Here,orHere}/hello.txt", "{startIndex:4,length:13,options:['Here','orHere']}")] // Folder
        [TestCase("Dir/Here/{hello,goodbye}.txt", "{startIndex:9,length:15,options:['hello','goodbye']}")] // File
        [TestCase("Dir/Here/{hello,goodbye,salut}.txt", "{startIndex:9,length:21,options:['hello','goodbye','salut']}")] // Three options
        [TestCase("Dir/{Here,orHere}/{hello,goodbye}.txt",
            "{startIndex:4,length:13,options:['Here','orHere']};" +
            "{startIndex:18,length:15,options:['hello','goodbye']}")] // Both Folder and File
        [TestCase("Dir/{H*e$re,orH|er?e}/hello.txt",
            "{startIndex:4,length:17,options:['H*e$re','orH|er?e']}")] // With other characters
        [TestCase("Dir/{he{r,e}or,Here}/hello.txt", "{startIndex:7,length:5,options:['r','e']}")] // Outer {} are ignored.
        public void GetStringGroups_ForGivenPath_ReturnsExpectedGroups(string path, string groupJsonStrings)
        {
            var expectedGroups = groupJsonStrings.IsNullOrEmpty()
                ? Enumerable.Empty<Group>()
                : groupJsonStrings.Split(';').Select(JsonConvert.DeserializeObject<Group>);

            var resultGroups = StringGroupRetriever.GetStringGroups(path);

            resultGroups.Should().BeEquivalentTo(expectedGroups);
        }
    }
}