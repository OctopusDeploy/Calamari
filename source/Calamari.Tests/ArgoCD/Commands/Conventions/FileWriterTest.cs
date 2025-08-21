using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class FileWriterTest
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        
        InMemoryLog log;
        string tempDirectory;
        string packageDirectory;
        string destinationDirectory;

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            packageDirectory = Path.Combine(tempDirectory, "package");
            destinationDirectory = Path.Combine(packageDirectory, "destination");
        }
        
        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }
        //
        // [Test]
        // public void WhenApplyFilesToASubDirectory_FilenamesReturnedAreRelativeToRoot()
        // {
        //     PackageRelativeFile[] filesToCopy = {
        //         new PackageRelativeFile(Path.Combine(packageDirectory, "firstFile.txt"), "firstFile.text"),
        //         new PackageRelativeFile(Path.Combine(packageDirectory, "nested", "firstFile.txt"), "secondFile.text"),
        //
        //     };
        //     
        //     foreach (var fileToCopy in filesToCopy)
        //     {
        //         Directory.CreateDirectory(Path.GetDirectoryName(fileToCopy.AbsolutePath)!);
        //         File.WriteAllText(fileToCopy.AbsolutePath, "arbitraryContent");
        //     }
        //     
        //     var fileWriter = new FileWriter(fileSystem, filesToCopy);
        //
        //     var subFolder = "";
        //     var result = fileWriter.CopyPackageFilesInto(destinationDirectory, subFolder);
        //     result.Should().HaveCount(2);
        //     result.Should().BeEquivalentTo(Path.Combine(subFolder, filesToCopy[0].PackageRelativePath),
        //                                    Path.Combine(subFolder, filesToCopy[1].PackageRelativePath));
        //     
        //     File.Exists(Path.Combine(destinationDirectory, subFolder, filesToCopy[0].PackageRelativePath));
        //     File.Exists(Path.Combine(destinationDirectory, subFolder, filesToCopy[1].PackageRelativePath));
        // }
    }
}