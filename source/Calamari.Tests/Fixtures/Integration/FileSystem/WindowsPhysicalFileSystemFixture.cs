using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class WindowsPhysicalFileSystemFixture
    {
        [Test]
        [TestCase("C:\\foo.bar", "C:\\foo.bar", ExpectedResult = "foo.bar")]
        [TestCase("C:\\foo.bar", "C:\\dir\\foo.bar", ExpectedResult = "dir\\foo.bar")]
        [TestCase("C:\\folder\\foo.bar", "C:\\foo.bar", ExpectedResult = "..\\foo.bar")]
        [TestCase("C:\\folder\\foo.bar", "C:\\dir\\foo.bar", ExpectedResult = "..\\dir\\foo.bar")]        
        public string ShouldGetRelativePath(string fromPath, string toPath)
        {
            var target = new WindowsPhysicalFileSystem();
            return target.GetRelativePath(fromPath, toPath);
        }
    }
}