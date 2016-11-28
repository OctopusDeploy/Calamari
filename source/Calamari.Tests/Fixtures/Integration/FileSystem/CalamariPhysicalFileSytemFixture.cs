using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    public class CalamariPhysicalFileSytemFixture
    {
        static readonly string PurgeTestDirectory = TestEnvironment.GetTestPath("PurgeTestDirectory");

        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(PurgeTestDirectory))
                Directory.Delete(PurgeTestDirectory, true);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void WindowsUsesWindowsFileSystem()
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            Assert.IsInstanceOf<WindowsPhysicalFileSystem>(fileSystem);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void NonWindowsUsesWindowsFileSystem()
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            Assert.IsInstanceOf<NixCalamariPhysicalFileSystem>(fileSystem);
        }

        [Test]
        public void PurgeWithNoExcludeRemovesAll()
        {
            CreateFile("ImportantFile.txt");
            CreateFile("MyDirectory", "SubDirectory", "WhoCaresFile.txt");
            CollectionAssert.IsNotEmpty(Directory.EnumerateFileSystemEntries(PurgeTestDirectory).ToList(), "Expected all files to have been set up");

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            fileSystem.PurgeDirectory(PurgeTestDirectory, FailureOptions.IgnoreFailure);

            CollectionAssert.IsEmpty(Directory.EnumerateFileSystemEntries(PurgeTestDirectory).ToList(), "Expected all items to be removed");
        }

        [Test]
        public void PurgeCanExcludeFile()
        {          
            var importantFile = CreateFile("ImportantFile.txt");
            var purgableFile = CreateFile("WhoCaresFile.txt");

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            fileSystem.PurgeDirectory(PurgeTestDirectory, (fsi) => fsi.Name.StartsWith("Important"), FailureOptions.IgnoreFailure);

            Assert.IsTrue(File.Exists(importantFile), $"Expected file `{importantFile}` to be preserved.");
            Assert.IsFalse(File.Exists(purgableFile), $"Expected file `{purgableFile}` to be removed.");
        }

        [Test]
        [TestCase("ImportantFolder", "WhoCaresFile", Description = "Purgable file in important folder should be kept", ExpectedResult = true)]
        [TestCase("ImportantFolder", "ImportantFile", Description = "Purgable file in important folder should still be kept", ExpectedResult = true)]
        [TestCase("WhoCaresFolder", "WhoCaresFile", Description = "Important file in purgable folder should still be removed", ExpectedResult = false)]
        [TestCase("WhoCaresFolder", "ImportantFile", Description = "Purgable file in purgable folder should be removed", ExpectedResult = false)]
        public bool PurgeDirectoryWithFolderExclusionWillNotCheckSubFiles(string folderName, string fileName)
        {
            var testFile = CreateFile(folderName, fileName);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            fileSystem.PurgeDirectory(PurgeTestDirectory, (fsi) => fsi.IsDirectory && fsi.Name.StartsWith("Important"), FailureOptions.IgnoreFailure);

            return File.Exists(testFile);
        }

        string CreateFile(params string[] relativePath)
        {
            var filename = Path.Combine(PurgeTestDirectory, Path.Combine(relativePath));

            var directory = Path.GetDirectoryName(filename);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(filename, new byte[] { 0 });
            return filename;
        }

    }
}
