using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
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
        [TestCase("WhoCaresFolder", "SimilarFile", "Similar*", Description = "Similar file in purgable folder should be removed", ExpectedResult = false)]
        [TestCase("WhoCaresFolder", "WhoCaresFile", "**/Similar*", Description = "Different file in different folder should be removed", ExpectedResult = false)]
        [TestCase("WhoCaresFolder", "SimilarFile", "**/Similar*", Description = "Similar file in different folder should be kept", ExpectedResult = true)]
        [TestCase("ExactFolder", "WhoCaresFile", "ExactFolder", Description = "Different file in exact folder should be kept", ExpectedResult = true)]
        public bool PurgeDirectoryWithFolderUsingGlobs(string folderName, string fileName, string glob)
        {
            var testFile = CreateFile(folderName, fileName);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            fileSystem.PurgeDirectory(PurgeTestDirectory, FailureOptions.IgnoreFailure, GlobMode.GroupExpansionMode, glob);

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
        [TestCase(@"Config/Feature1/*-{a,b}.config", "f1-a.config", 2, 2)]
        [TestCase(@"Config/Feature1/*-{a,b}.config", "f1-b.config", 2, 2)]
        [TestCase(@"Config/Feature1/f1-{a,b}.config", "f1-a.config", 2, 0)]
        [TestCase(@"Config/Feature1/f1-{a,b}.config", "f1-b.config", 2, 0)]
        [TestCase(@"Config/Feature{1,2}/f{1,2}.{config,txt}", "f1.txt", 2, 0)]
        [TestCase(@"Config/Feature{1,2}/f{1,2}.{config,txt}", "f2.config", 2, 0)]
        [TestCase(@"Config/Feature1/*-[ab].config", "f1-a.config", 2, 0)]
        [TestCase(@"Config/Feature1/*-[ab].config", "f1-b.config", 2, 0)]
        [TestCase(@"Config/Feature1/f1-[ab].config", "f1-a.config", 2, 0)]
        [TestCase(@"Config/Feature1/f1-[ab].config", "f1-b.config", 2, 0)]
        [TestCase(@"Config/Feature[12]/f[12].{config,txt}", "f1.txt", 2, 0)]
        [TestCase(@"Config/Feature[12]/f[12].{config,txt}", "f2.config", 2, 0)]
        [TestCase(@"Config/Feature1/f1-[a-c].{config,txt}", "f1-b.config", 3, 0)]
        public void EnumerateFilesWithGlob(string pattern, string expectedFileMatchName, int expectedQty = 1, int? expectedQtyWithNoGrouping = null)
        {
            expectedQtyWithNoGrouping = expectedQtyWithNoGrouping ?? expectedQty;
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

            var result = fileSystem.EnumerateFilesWithGlob(rootPath, GlobMode.GroupExpansionMode, pattern).ToList();

            var resultNoGrouping = fileSystem.EnumerateFilesWithGlob(rootPath, GlobMode.LegacyMode, pattern).ToList();

            resultNoGrouping.Should()
                            .HaveCount(expectedQtyWithNoGrouping.Value,
                $"{pattern} should have found {expectedQtyWithNoGrouping}, but found {result.Count}");

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

            var results = fileSystem.EnumerateFilesWithGlob(rootPath, GlobMode.GroupExpansionMode, pattern).ToArray();

            if (results.Length > 0)
                results.Should().OnlyContain(f => f.EndsWith("File"));
        }

        [Test]
        public void EnumerateFilesWithGlobShouldNotReturnTheSameFileTwice()
        {
            File.WriteAllText(Path.Combine(rootPath, "File"), "");

            var results = fileSystem.EnumerateFilesWithGlob(rootPath, GlobMode.GroupExpansionMode, "*", "**").ToList();

            results.Should().HaveCount(1);
        }

        [TestCase(@"*")]
        [TestCase(@"**")]
        [TestCase(@"Dir/*")]
        [TestCase(@"Dir/**")]
        public void EnumerateFilesShouldNotReturnDirectories(string pattern)
        {
            Directory.CreateDirectory(Path.Combine(rootPath, "Dir"));
            Directory.CreateDirectory(Path.Combine(rootPath, "Dir", "Sub"));
            File.WriteAllText(Path.Combine(rootPath, "Dir", "File"), "");
            File.WriteAllText(Path.Combine(rootPath, "Dir", "Sub", "File"), "");

            var results = fileSystem.EnumerateFiles(rootPath, pattern).ToArray();

            if (results.Length > 0)
                results.Should().OnlyContain(f => f.EndsWith("File"));
        }

        [Test]
        public void EnumerateFilesWithShouldNotReturnTheSameFileTwice()
        {
            File.WriteAllText(Path.Combine(rootPath, "File"), "");

            var results = fileSystem.EnumerateFiles(rootPath, "*", "**").ToList();

            results.Should().HaveCount(1);
        }

        [Test]
        [TestCase(new[] { @"*.txt" }, new [] {"r.txt"}, 1)]
        [TestCase(new[] { @"*.config" }, new [] {"root.config"}, 1)]
        [TestCase(new[] { @"*.config", @"*.txt", @"*" }, new [] {"root.config", "r.txt"}, 2)]
        [TestCase(new[] { @"Config/*.config"}, new [] {"c.config"}, 1)]
        [TestCase(new[] { @"Config/Feature1/*.config"}, new [] {"f1-a.config", "f1-b.config", "f1-c.config"}, 3)]
        [TestCase(new[] { @"Config/Feature2/*.config"}, new [] {"f2.config"}, 1)]
        [TestCase(new[] { @"Config/Feature2/Feature2-SubFolder/*.config"}, new[] {"f2-sub.config"}, 1)]
        public void EnumerateFiles(string[] pattern, string[] expectedFilesMatchName, int expectedQty)
        {
            var content = "file-content" + Environment.NewLine;

            var configPath = Path.Combine(rootPath, "Config");

            Directory.CreateDirectory(configPath);
            Directory.CreateDirectory(Path.Combine(configPath, "Feature1"));
            Directory.CreateDirectory(Path.Combine(configPath, "Feature2"));
            Directory.CreateDirectory(Path.Combine(configPath, "Feature2", "Feature2-SubFolder"));

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
            writeFile(configPath, "Feature1", "f2.txt");
            writeFile(configPath, Path.Combine("Feature2", "Feature2-SubFolder"), "f2-sub.config");
            writeFile(configPath, Path.Combine("Feature2", "Feature2-SubFolder"), "f2-sub.txt");

            var resultWithoutRecursion = fileSystem.EnumerateFiles(rootPath, pattern).ToList();

            resultWithoutRecursion.Should()
                                  .HaveCount(expectedQty, $"{pattern} should have found {expectedQty}, but found {resultWithoutRecursion.Count}");
            foreach (var expectedFileName in expectedFilesMatchName)
            {
                resultWithoutRecursion.Should()
                                      .Contain(r => Path.GetFileName(r) == expectedFileName, $"{pattern} should have found {expectedFileName}, but didn't");
            }
        }

        [Test, RequiresNonMono]
        [TestCase(new [] { @"*.txt" }, new [] {"r.txt", "f1.txt", "f2.txt", "f2-sub.txt"}, 4)]
        [TestCase(new [] { @"*.config" }, new [] { "root.config", "c.config", "f1-a.config", "f1-b.config", "f1-c.config", "f2.config", "f2-sub.config"}, 7)]
        [TestCase(new [] { @"*.config" , "*.txt", "*" }, new [] { "r.txt", "f1.txt", "f2.txt", "f2-sub.txt", "root.config", "c.config", "f1-a.config", "f1-b.config", "f1-c.config", "f2.config", "f2-sub.config"}, 11)]
        [TestCase(new [] { @"Config/*.config" }, new [] { "c.config", "f1-a.config", "f1-b.config", "f1-c.config", "f2.config", "f2-sub.config"}, 6)]
        [TestCase(new [] { @"Config/Feature1/*.config" }, new[] {"f1-a.config", "f1-b.config", "f1-c.config"}, 3)]
        [TestCase(new [] { @"Config/Feature2/*.config" }, new[] {"f2.config", "f2-sub.config"}, 2)]
        [TestCase(new [] { @"Config/Feature2/Feature2-SubFolder/*.config" },new[] {"f2-sub.config"}, 1)]
        public void EnumerateFilesRecursively(string[] patterns, string[] expectedFilesMatchNameWithRecursion, int expectedQty)
        {
            var content = "file-content" + Environment.NewLine;

            var configPath = Path.Combine(rootPath, "Config");

            Directory.CreateDirectory(configPath);
            Directory.CreateDirectory(Path.Combine(configPath, "Feature1"));
            Directory.CreateDirectory(Path.Combine(configPath, "Feature2"));
            Directory.CreateDirectory(Path.Combine(configPath, "Feature2", "Feature2-SubFolder"));

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
            writeFile(configPath, "Feature1", "f2.txt");
            writeFile(configPath, Path.Combine("Feature2", "Feature2-SubFolder"), "f2-sub.config");
            writeFile(configPath, Path.Combine("Feature2", "Feature2-SubFolder"), "f2-sub.txt");

            var resultWithRecursion = fileSystem.EnumerateFilesRecursively(rootPath, patterns).ToList();

            resultWithRecursion.Should()
                               .HaveCount(expectedQty,
                                          $"{patterns} should have found {expectedQty}, but found {resultWithRecursion.Count}");
            
            foreach (var expectedFileName in expectedFilesMatchNameWithRecursion)
            {
                resultWithRecursion.Should()
                                   .Contain(r => Path.GetFileName(r) == expectedFileName, $"{patterns} should have found {expectedFileName}, but didn't");
            }
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

            var results = fileSystem.EnumerateFilesWithGlob(rootPath, GlobMode.GroupExpansionMode, glob).ToList();
            var resultsWithNoGrouping = fileSystem.EnumerateFilesWithGlob(rootPath, GlobMode.LegacyMode, glob).ToList();

            results.Should().ContainSingle();
            resultsWithNoGrouping.Should().ContainSingle();

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
