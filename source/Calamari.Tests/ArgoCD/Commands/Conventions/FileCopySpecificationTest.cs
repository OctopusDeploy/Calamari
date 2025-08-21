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
            
            copyInto.DestinationAbsolutePath.Should().Be("/MyNewRootArea/subFolder/file");
            copyInto.DestinationRelativePath.Should().Be("subFolder/file");
        }
        
        [Test]
        public void MultiLayerSubFolderAreCorrectlyDetermined()
        {
            var originalPackage = new PackageRelativeFile("/tmp/unzippedPackage/file", "file");
            var copyInto = new FileCopySpecification(originalPackage, "/MyNewRootArea/", "subFolder/AndThenMore");
            
            copyInto.DestinationAbsolutePath.Should().Be("/MyNewRootArea/subFolder/AndThenMore/file");
            copyInto.DestinationRelativePath.Should().Be("subFolder/AndThenMore/file");
        }

        [Test]
        public void DotSlashSubFolderIsPropogatedToDestinationPath()
        {
            var originalPackage = new PackageRelativeFile("/tmp/unzippedPackage/file", "file");
            var copyInto = new FileCopySpecification(originalPackage, "/MyNewRootArea/", "./");
            
            copyInto.DestinationAbsolutePath.Should().Be("/MyNewRootArea/./file");
            copyInto.DestinationRelativePath.Should().Be("./file");
        }
    }
}