using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class WindowsPhysicalFileSystemFixture
    {
        [Test]
        [TestCase("C:\\foo.bar", "C:\\foo.bar", Result = "foo.bar")]
        [TestCase("C:\\foo.bar", "C:\\dir\\foo.bar", Result = "dir\\foo.bar")]
        [TestCase("C:\\folder\\foo.bar", "C:\\foo.bar", Result = "..\\foo.bar")]
        [TestCase("C:\\folder\\foo.bar", "C:\\dir\\foo.bar", Result = "..\\dir\\foo.bar")]        
        public string ShouldGetRelativePath(string fromPath, string toPath)
        {
            var target = new WindowsPhysicalFileSystem();
            return target.GetRelativePath(fromPath, toPath);
        }
    }
}