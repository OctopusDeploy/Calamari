using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using FluentAssertions;
using NUnit.Framework;

namespace Sashimi.Template.Wrangler.Tests
{
    [TestFixture]
    public class WranglerTests
    {
        TemporaryDirectory installFolder;

        [OneTimeSetUp]
        public async Task Setup()
        {
            installFolder = TemporaryDirectory.Create();
            var currentPath = Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path).Replace('/', Path.DirectorySeparatorChar));

            var templatePath = Path.GetFullPath(Path.Combine(currentPath, "../../../../NewStep/Sashimi.NamingIsHard"));

            Copy(templatePath, installFolder.DirectoryPath);

            await Program.Main(new[] { "1.0.0", installFolder.DirectoryPath });
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            installFolder?.Dispose();
        }

        [Test]
        public void CreatesSolution()
        {
            var slnFile = Path.Combine(installFolder.DirectoryPath, "source/Sashimi.NamingIsHard.sln");

            File.Exists(slnFile).Should().BeTrue();
        }

        [Test]
        public void UpdatedCsprojToIncludeOnlyProjectReferencesBetweenEachProject()
        {
            var csProjFiles = Directory.EnumerateFiles(Path.Combine(installFolder.DirectoryPath, "source"), "*.csproj", SearchOption.AllDirectories);
            foreach (var projFile in csProjFiles)
            {
                var content = File.ReadAllText(projFile);
                content.Should().NotContain("<ProjectReference Include=\"..\\..\\");
            }
        }

        void Copy(string sourcePath, string destinationPath)
        {
            foreach (var dirPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));

            foreach (var newPath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
        }
    }
}