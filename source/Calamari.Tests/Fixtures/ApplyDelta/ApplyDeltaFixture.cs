using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.ServiceMessages;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ApplyDelta
{
    [TestFixture]
    public class ApplyDeltaFixture : CalamariFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "ApplyDelta");
        static  readonly string DownloadPath = Path.Combine(TentacleHome, "Files");

        const string NewFileName = "Acme.Web.1.0.0.1.nupkg";

        CalamariResult ApplyDelta(string basisFile, string fileHash, string deltaFile, string newFile)
        {
            return Invoke(Calamari()
                .Action("apply-delta")
                .Argument("basisFileName", basisFile)
                .Argument("fileHash", fileHash)
                .Argument("deltaFileName", deltaFile)
                .Argument("newFileName", newFile));
        }

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(DownloadPath))
                Directory.CreateDirectory(DownloadPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(DownloadPath))
                Directory.Delete(DownloadPath, true);
        }

        [Test]
        public void ShouldApplyDeltaToPreviousPackageToCreateNewPackage()
        {
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0.0")))
            using (var signatureFile = new TemporaryFile(basisFile.FilePath + ".octosig"))
            {
                var signatureResult = Invoke(OctoDiff()
                    .Action("signature")
                    .PositionalArgument(basisFile.FilePath)
                    .PositionalArgument(signatureFile.FilePath));
                
                signatureResult.AssertZero();
                Assert.That(File.Exists(signatureFile.FilePath));

                using (var newFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0.1", true)))
                using (var deltaFile = new TemporaryFile(basisFile.FilePath + "_to_" + NewFileName + ".octodelta"))
                {
                    var deltaResult = Invoke(OctoDiff()
                        .Action("delta")
                        .PositionalArgument(signatureFile.FilePath)
                        .PositionalArgument(newFile.FilePath)
                        .PositionalArgument(deltaFile.FilePath));

                    deltaResult.AssertZero();
                    Assert.That(File.Exists(deltaFile.FilePath));

                    var patchResult = ApplyDelta(basisFile.FilePath, basisFile.Hash, deltaFile.FilePath, NewFileName);
                    patchResult.AssertZero();
                    patchResult.AssertOutput("Applying delta to {0} with hash {1} and storing as {2}", basisFile.FilePath,
                        basisFile.Hash, Path.Combine(DownloadPath, NewFileName));
                    patchResult.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name);
                }
            }
        }

        [Test]
        public void ShouldFailWhenNoBasisFileIsSpecified()
        {
            var result = ApplyDelta("", "Hash", "Delta", "New");

            result.AssertNonZero();
            result.AssertErrorOutput("No basis file was specified. Please pass --basisFileName MyPackage.1.0.0.0.nupkg");
        }

        [Test]
        public void ShouldFailWhenNoFileHashIsSpecified()
        {
            var result = ApplyDelta("Basis", "", "Delta", "New");

            result.AssertNonZero();
            result.AssertErrorOutput("No file hash was specified. Please pass --fileHash MyFileHash");
        }
        [Test]
        public void ShouldFailWhenNoDeltaFileIsSpecified()
        {
            var result = ApplyDelta("Basis", "Hash", "", "New");

            result.AssertNonZero();
            result.AssertErrorOutput("No delta file was specified. Please pass --deltaFileName MyPackage.1.0.0.0_to_1.0.0.1.octodelta");
        }

        [Test]
        public void ShouldFailWhenNoNewFileIsSpecified()
        {
            var result = ApplyDelta("Basis", "Hash", "Delta", "");

            result.AssertNonZero();
            result.AssertErrorOutput("No new file name was specified. Please pass --newFileName MyPackage.1.0.0.1.nupkg");
        }

        [Test]
        public void ShouldFailWhenBasisFileCannotBeFound()
        {
            var basisFile = Path.Combine(DownloadPath, "MyPackage.1.0.0.0.nupkg");
            var result = ApplyDelta(basisFile, "Hash", "Delta", "New");

            result.AssertNonZero();
            result.AssertErrorOutput("Could not find basis file: " + basisFile);
        }

        [Test]
        public void ShouldFailWhenDeltaFileCannotBeFound()
        {
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0.0")))
            {
                var deltaFilePath = Path.Combine(DownloadPath, "Acme.Web.1.0.0.0_to_1.0.0.1.octodelta");
                var result = ApplyDelta(basisFile.FilePath, basisFile.Hash, deltaFilePath, "New");

                result.AssertNonZero();
                result.AssertErrorOutput("Could not find delta file: " + deltaFilePath);
            }
        }

        [Test]
        public void ShouldFailWhenBasisFileHashDoesNotMatchSpecifiedFileHash()
        {
            var otherBasisFileHash = "2e9407c9eae20ffa94bf050283f9b4292a48504c";
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0.0")))
            {
                var deltaFilePath = Path.Combine(DownloadPath,
                    "Acme.Web.1.0.0.0_to_1.0.0.1.octodelta");
                using (var deltaFile = File.CreateText(deltaFilePath))
                {
                    deltaFile.WriteLine("This is a delta file!");
                    deltaFile.Flush();
                }

                var result = ApplyDelta(basisFile.FilePath, otherBasisFileHash, deltaFilePath, NewFileName);

                result.AssertNonZero();
                result.AssertErrorOutput("Basis file hash {0} does not match the file hash specified {1}", basisFile.Hash, otherBasisFileHash);
            }
        }

        [Test]
        public void ShouldFailWhenDeltaFileIsInvalid()
        {
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0.0")))
            {
                var deltaFilePath = Path.Combine(DownloadPath,
                    "Acme.Web.1.0.0.0_to_1.0.0.1.octodelta");
                using (var deltaFile = File.CreateText(deltaFilePath))
                {
                    deltaFile.WriteLine("This is a delta file!");
                    deltaFile.Flush();
                }

                var result = ApplyDelta(basisFile.FilePath, basisFile.Hash, deltaFilePath, NewFileName);

                result.AssertNonZero();
                result.AssertOutput("Applying delta to {0} with hash {1} and storing as {2}",
                    basisFile.FilePath,
                    basisFile.Hash,
                    Path.Combine(DownloadPath, NewFileName));
                result.AssertOutput("The delta file appears to be corrupt.");
            }
        }
    }
}
