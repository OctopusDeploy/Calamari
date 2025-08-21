using System;
using System.IO;
using Calamari.ArgoCD.Conventions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class FileCopySpecificationTest
    {
        [Test]
        public void DestinationPathsAreCorrectlyDetermined()
        {
            var originalPackage = new PackageRelativeFile("/tmp/unzippedPackage/file", "file");
            var copyInto = new FileCopySpecification(originalPackage, "/MyNewRootArea/", "subFolder");

            copyInto.DestinationAbsolutePath.Should().Be($"/MyNewRootArea{Path.DirectorySeparatorChar}subFolder{Path.DirectorySeparatorChar}file");
            copyInto.DestinationRelativePath.Should().Be($"subFolder{Path.DirectorySeparatorChar}file");
        }
        
        [Test]
        public void MultiLayerSubFolderAreCorrectlyDetermined()
        {
            var originalPackage = new PackageRelativeFile("/tmp/unzippedPackage/file", "file");
            var copyInto = new FileCopySpecification(originalPackage, "/MyNewRootArea/", "subFolder/AndThenMore");
            
            copyInto.DestinationAbsolutePath.Should().Be($"/MyNewRootArea{Path.DirectorySeparatorChar}subFolder/AndThenMore{Path.DirectorySeparatorChar}file");
            copyInto.DestinationRelativePath.Should().Be($"subFolder/AndThenMore{Path.DirectorySeparatorChar}file");
        }

        [Test]
        public void DotSlashSubFolderIsPropagatedToDestinationPath()
        {
            var originalPackage = new PackageRelativeFile("/tmp/unzippedPackage/file", "file");
            var copyInto = new FileCopySpecification(originalPackage, "/MyNewRootArea", "./");
            
            copyInto.DestinationAbsolutePath.Should().Be($"/MyNewRootArea{Path.DirectorySeparatorChar}./file");
            copyInto.DestinationRelativePath.Should().Be("./file");
        }
    }
}