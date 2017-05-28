using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    public class PathExtensionsFixture
    {
        [TestCase(@"c:\foo", @"c:\foo", true)]
        [TestCase(@"c:\foo", @"c:\foo\", true)]
        [TestCase(@"c:\foo\", @"c:\foo", true)]
        [TestCase(@"c:\foo\bar\", @"c:\foo\", true)]
        [TestCase(@"c:\foo\bar", @"c:\foo\", true)]
        [TestCase(@"c:\foo\a.txt", @"c:\foo", true)]
        [TestCase(@"c:/foo/a.txt", @"c:\foo", true)]
        [TestCase(@"c:\foobar", @"c:\foo", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo\", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\foobar", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\foobar\", false)]
        [TestCase(@"c:\foo\..\bar\baz", @"c:\foo", false)]
        [TestCase(@"c:\foo\..\bar\baz", @"c:\bar", true)]
        [TestCase(@"c:\foo\..\bar\baz", @"c:\barr", false)]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void IsChildOfTest(string child, string parent, bool result)
        {
            child.IsChildOf(parent).Should().Be(result);
        }

        [TestCase(@"c:\FOO\a.txt", @"c:\foo", true)]
        [TestCase(@"c:\foo\a.txt", @"c:\FOO", true)]
        [TestCase(@"c:\foo", @"c:", true)]
        [TestCase(@"c:\foo", @"c:\", true)]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void IsChildOfTestWindows(string child, string parent, bool result)
        {
            child.IsChildOf(parent).Should().Be(result);
        }

        [TestCase(@"c:\FOO\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\FOO", false)]
        [TestCase(@"/", @"/", true)]
        [TestCase(@"/foo", @"/", true)]
        [TestCase(@"/", @"/foo", false)]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void IsChildOfTestUnix(string child, string parent, bool result)
        {
            child.IsChildOf(parent).Should().Be(result);
        }
    }
}