using NUnit.Framework;
using Calamari.Integration.FileSystem;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
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