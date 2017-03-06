using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        [Test]
        [TestCase(@"**/*.txt", "f1.txt", 2)]
        [TestCase(@"**/*.txt", "r.txt", 2)]
        [TestCase(@"*.txt", "r.txt")]
        [TestCase(@"**/*.config", "root.config", 5)]
        [TestCase(@"*.config", "root.config")]
        [TestCase(@"Config/*.config", "c.config")]
        [TestCase(@"Config/Feature1/*.config", "f1-a.config", 2)]
        [TestCase(@"Config/Feature1/*.config", "f1-b.config", 2)]
        [TestCase(@"Config/Feature2/*.config", "f2.config")]
        public void GlobTestMutiple(string pattern, string expectedFileMatchName, int expectedQty = 1)
        {
            var realFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var rootPath = realFileSystem.CreateTemporaryDirectory();
            var content = "file-content" + Environment.NewLine;

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                pattern = pattern.Replace(@"/", @"\");
            }

            try
            {
                var configPath = Path.Combine(rootPath, "Config");

                realFileSystem.CreateDirectory(configPath);
                realFileSystem.CreateDirectory(Path.Combine(configPath, "Feature1"));
                realFileSystem.CreateDirectory(Path.Combine(configPath, "Feature2"));

                Action<string, string, string> writeFile = (p1, p2, p3) =>
                    realFileSystem.OverwriteFile(p3 == null ? Path.Combine(p1, p2) : Path.Combine(p1, p2, p3), content);

                // NOTE: create all the files in *every case*, and TestCases help supply the assert expectations
                writeFile(rootPath, "root.config", null);
                writeFile(rootPath, "r.txt", null);
                writeFile(configPath, "c.config", null);

                writeFile(configPath, "Feature1", "f1.txt");
                writeFile(configPath, "Feature1", "f1-a.config");
                writeFile(configPath, "Feature1", "f1-b.config");
                writeFile(configPath, "Feature2", "f2.config");

                var result = Glob.Expand(Path.Combine(rootPath, pattern)).ToList();

                Assert.AreEqual(expectedQty, result.Count, $"{pattern} should have found {expectedQty}, but found {result.Count}");
                Assert.True(result.Any(r => r.Name.Equals(expectedFileMatchName)), $"{pattern} should have found {expectedFileMatchName}, but didn't");
            }
            finally
            {
                realFileSystem.DeleteDirectory(rootPath);
            }
        }
    }
}
