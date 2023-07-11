using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    public class CalamariPhysicalFileSystemFixture
    {
        static readonly string PurgeTestDirectory = TestEnvironment.GetTestPath("PurgeTestDirectory");
        private CalamariPhysicalFileSystem fileSystem;
        private string rootPath;

        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(PurgeTestDirectory))
                Directory.Delete(PurgeTestDirectory, true);

            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            rootPath = Path.GetTempFileName();
            File.Delete(rootPath);
            Directory.CreateDirectory(rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(rootPath, true);
        }


        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void WindowsUsesWindowsFileSystem()
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            Assert.IsInstanceOf<WindowsPhysicalFileSystem>(fileSystem);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
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
            fileSystem.PurgeDirectory(PurgeTestDirectory, (fsi) => fsi.Attributes.HasFlag(FileAttributes.Directory) && fsi.Name.StartsWith("Important"), FailureOptions.IgnoreFailure);

            return File.Exists(testFile);
        }

        [Test]
        [TestCase("SimilarFolder", "WhoCaresFile", "Similar*", Description = "Different file in Similar folder should be kept", ExpectedResult = true)]
        [TestCase("SimilarFolder", "SimilarFile", "Similar*", Description = "Similar file in Similar folder should still be kept", ExpectedResult = true)]
        [TestCase("WhoCaresFolder", "WhoCaresFile", "Similar*", Description = "Similar file in purgable folder should still be removed", ExpectedResult = false)]
        [TestCase("WhoCaresFolder", "SimilarFile", "Similar*", Description = "Different file in purgable folder should be removed", ExpectedResult = false)]
        [TestCase("WhoCaresFolder", "WhoCaresFile", "**/Similar*", Description = "Different file in different folder should be removed", ExpectedResult = false)]
        [TestCase("WhoCaresFolder", "SimilarFile", "**/Similar*", Description = "Similar file in different folder should be kept", ExpectedResult = true)]
        [TestCase("ExactFolder", "WhoCaresFile", "ExactFolder", Description = "Different file in exact folder should be kept", ExpectedResult = true)]
        public bool PurgeDirectoryWithFolderUsingGlobs(string folderName, string fileName, string glob)
        {
            var testFile = CreateFile(folderName, fileName);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            fileSystem.PurgeDirectory(PurgeTestDirectory, FailureOptions.IgnoreFailure, glob);

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
        [TestCase(@"**/*.config", "root.config", 6)]
        [TestCase(@"*.config", "root.config")]
        [TestCase(@"Config/*.config", "c.config")]
        [TestCase(@"Config/Feature1/*.config", "f1-a.config", 3)]
        [TestCase(@"Config/Feature1/*.config", "f1-b.config", 3)]
        [TestCase(@"Config/Feature1/*.config", "f1-c.config", 3)]
        [TestCase(@"Config/Feature2/*.config", "f2.config")]
        [TestCase(@"Config/Feature1/*-{a,b}.config", "f1-a.config", 2)]
        [TestCase(@"Config/Feature1/*-{a,b}.config", "f1-b.config", 2)]
        [TestCase(@"Config/Feature1/f1-{a,b}.config", "f1-a.config", 2)]
        [TestCase(@"Config/Feature1/f1-{a,b}.config", "f1-b.config", 2)]
        [TestCase(@"Config/Feature{1,2}/f{1,2}.{config,txt}", "f1.txt", 2)]
        [TestCase(@"Config/Feature{1,2}/f{1,2}.{config,txt}", "f2.config", 2)]
        [TestCase(@"Config/Feature1/*-[ab].config", "f1-a.config", 2)]
        [TestCase(@"Config/Feature1/*-[ab].config", "f1-b.config", 2)]
        [TestCase(@"Config/Feature1/f1-[ab].config", "f1-a.config", 2)]
        [TestCase(@"Config/Feature1/f1-[ab].config", "f1-b.config", 2)]
        [TestCase(@"Config/Feature[12]/f[12].{config,txt}", "f1.txt", 2)]
        [TestCase(@"Config/Feature[12]/f[12].{config,txt}", "f2.config", 2)]
        [TestCase(@"Config/Feature1/f1-[a-c].{config,txt}", "f1-b.config", 3)]
        public void EnumerateFilesWithGlob(string pattern, string expectedFileMatchName, int expectedQty = 1)
        {
            var content = "file-content" + Environment.NewLine;

            var configPath = Path.Combine(rootPath, "Config");

            Directory.CreateDirectory(configPath);
            Directory.CreateDirectory(Path.Combine(configPath, "Feature1"));
            Directory.CreateDirectory(Path.Combine(configPath, "Feature2"));

            Action<string, string, string> writeFile = (p1, p2, p3) =>
                fileSystem.OverwriteFile(p3 == null ? Path.Combine(p1, p2) : Path.Combine(p1, p2, p3), content);

            // NOTE: create all the files in *every case*, and TestCases help supply the assert expectations
            writeFile(rootPath, "root.config", null);
            writeFile(rootPath, "r.txt", null);
            writeFile(configPath, "c.config", null);

            writeFile(configPath, "Feature1", "f1.txt");
            writeFile(configPath, "Feature1", "f1-a.config");
            writeFile(configPath, "Feature1", "f1-b.config");
            writeFile(configPath, "Feature1", "f1-c.config");
            writeFile(configPath, "Feature2", "f2.config");

            var result = fileSystem.EnumerateFilesWithGlob(rootPath, pattern).ToList();

            result.Should()
                .HaveCount(expectedQty, $"{pattern} should have found {expectedQty}, but found {result.Count}");
            result.Should()
                .Contain(r => Path.GetFileName(r) == expectedFileMatchName, $"{pattern} should have found {expectedFileMatchName}, but didn't");
        }

        [TestCase(@"*")]
        [TestCase(@"**")]
        [TestCase(@"**/*")]
        [TestCase(@"Dir/*")]
        [TestCase(@"Dir/**")]
        [TestCase(@"Dir/**/*")]
        public void EnumerateFilesWithGlobShouldNotReturnDirectories(string pattern)
        {
            Directory.CreateDirectory(Path.Combine(rootPath, "Dir"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Dir", "Sub"));
            File.WriteAllText(Path.Combine(rootPath, "Dir", "File"), "");
            File.WriteAllText(Path.Combine(rootPath, "Dir", "Sub", "File"), "");

            var results = fileSystem.EnumerateFilesWithGlob(rootPath, pattern).ToArray();

            if (results.Length > 0)
                results.Should().OnlyContain(f => f.EndsWith("File"));
        }

        [Test]
        public void EnumerateFilesWithGlobShouldNotReturnTheSameFileTwice()
        {
            File.WriteAllText(Path.Combine(rootPath, "File"), "");

            var results = fileSystem.EnumerateFilesWithGlob(rootPath, "*", "**").ToList();

            results.Should().HaveCount(1);
        }


        [TestCase(@"[Configuration]", @"[Configuration]\\*.txt")]
        [TestCase(@"Configuration]", @"Configuration]\\*.txt")]
        [TestCase(@"[Configuration", @"[Configuration\\*.txt")]
        [TestCase(@"{Configuration}", @"{Configuration}\\*.txt")]
        [TestCase(@"Configuration}", @"Configuration}\\*.txt")]
        [TestCase(@"{Configuration", @"{Configuration\\*.txt")]
        public void EnumerateFilesWithGlobShouldIgnoreGroups(string directory, string glob)
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
                glob = glob.Replace("\\", "/");

            Directory.CreateDirectory(Path.Combine(rootPath, directory));

            File.WriteAllText(Path.Combine(rootPath, directory, "Foo.txt"), "");

            var results = fileSystem.EnumerateFilesWithGlob(rootPath, glob).ToList();

            results.Should().HaveCount(1);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void LongFilePathsShouldWork()
        {
            var paths = new Stack<string>();
            var path = rootPath;

            for (var i = 0; i <= 15; i++)
            {
                path += @"\ZZZZabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";
                fileSystem.EnsureDirectoryExists(path);
                paths.Push(path);
            }

            fileSystem.OverwriteFile("Some sample text", path + @"\test.txt");
            fileSystem.DeleteFile(path + @"\test.txt");

            while (paths.Any())
            {
                var pathToRemove = paths.Pop();
                fileSystem.DeleteDirectory(pathToRemove);
            }
        }

        [Test]
        public void WriteAllTextShouldOverwriteHiddenFileContent()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "hi there");
            File.SetAttributes(path, FileAttributes.Hidden);
            fileSystem.WriteAllText(path, "hi");
            Assert.AreEqual("hi", File.ReadAllText(path));
            Assert.AreNotEqual(0, File.GetAttributes(path) & FileAttributes.Hidden);
        }

        [Test]
        public void WriteAllBytesShouldOverwriteHiddenFile()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "hi there");
            File.SetAttributes(path, FileAttributes.Hidden);
            fileSystem.WriteAllBytes(path, Encoding.ASCII.GetBytes("hi"));
            Assert.AreEqual("hi", File.ReadAllText(path));
            Assert.AreNotEqual(0, File.GetAttributes(path) & FileAttributes.Hidden);
        }
    }
}
