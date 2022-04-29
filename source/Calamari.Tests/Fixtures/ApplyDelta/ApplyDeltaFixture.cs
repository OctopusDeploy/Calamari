using System;
using System.IO;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ApplyDelta
{
    [TestFixture]
    public class ApplyDeltaFixture : CalamariFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "ApplyDelta");
        static  readonly string DownloadPath = Path.Combine(TentacleHome, "Files");

        const string NewFileName = "Acme.Web.1.0.1.nupkg";

        CalamariResult ApplyDelta(string basisFile, string fileHash, string deltaFile, string newFile, bool messageOnError = false)
        {
            var command = Calamari()
                .Action("apply-delta")
                .Argument("basisFileName", basisFile)
                .Argument("fileHash", fileHash)
                .Argument("deltaFileName", deltaFile)
                .Argument("newFileName", newFile);

            if (messageOnError)
                command = command.Flag("serviceMessageOnError");

            return Invoke(command);
        }

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
        }

        [OneTimeTearDown]
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
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0")))
            using (var signatureFile = new TemporaryFile(basisFile.FilePath + ".octosig"))
            {
#if USE_OCTODIFF_EXE
                var signatureResult = Invoke(OctoDiff()
                    .Action("signature")
                    .PositionalArgument(basisFile.FilePath)
                    .PositionalArgument(signatureFile.FilePath));

                signatureResult.AssertSuccess();
#else
                var exitCode = Octodiff.Program.Main(new[] {"signature", basisFile.FilePath, signatureFile.FilePath});
                Assert.That(exitCode, Is.EqualTo(0), string.Format("Expected command to return exit code 0, received {0}", exitCode));
#endif
                Assert.That(File.Exists(signatureFile.FilePath));

                using (var newFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.1", true)))
                using (var deltaFile = new TemporaryFile(basisFile.FilePath + "_to_" + NewFileName + ".octodelta"))
                {
#if USE_OCTODIFF_EXE
                    var deltaResult = Invoke(OctoDiff()
                        .Action("delta")
                        .PositionalArgument(signatureFile.FilePath)
                        .PositionalArgument(newFile.FilePath)
                        .PositionalArgument(deltaFile.FilePath));

                    deltaResult.AssertSuccess();
#else
                    var deltaExitCode = Octodiff.Program.Main(new[] { "delta", signatureFile.FilePath, newFile.FilePath, deltaFile.FilePath });
                    Assert.That(deltaExitCode, Is.EqualTo(0), string.Format("Expected command to return exit code 0, received {0}", exitCode));
#endif
                    Assert.That(File.Exists(deltaFile.FilePath));

                    var patchResult = ApplyDelta(basisFile.FilePath, basisFile.Hash, deltaFile.FilePath, NewFileName);
                    patchResult.AssertSuccess();

                    patchResult.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name);
                    patchResult.AssertOutput("Applying delta to {0} with hash {1} and storing as {2}", basisFile.FilePath,
                        basisFile.Hash, patchResult.CapturedOutput.DeltaVerification.FullPathOnRemoteMachine);
                    Assert.AreEqual(newFile.Hash, patchResult.CapturedOutput.DeltaVerification.Hash);
                    Assert.AreEqual(newFile.Hash, HashCalculator.Hash(patchResult.CapturedOutput.DeltaVerification.FullPathOnRemoteMachine));
                }
            }
        }

        [Test]
        public void ShouldWriteErrorWhenNoBasisFileIsSpecified()
        {
            var result = ApplyDelta("", "Hash", "Delta", "New");

            result.AssertSuccess();
            result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: "No basis file was specified. Please pass --basisFileName MyPackage.1.0.0.0.nupkg");
        }

        [Test]
        public void ShouldWriteErrorWhenNoFileHashIsSpecified()
        {
            var result = ApplyDelta("Basis", "", "Delta", "New");

            result.AssertSuccess();
            result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: "No file hash was specified. Please pass --fileHash MyFileHash");
        }
        [Test]
        public void ShouldWriteErrorWhenNoDeltaFileIsSpecified()
        {
            var result = ApplyDelta("Basis", "Hash", "", "New");

            result.AssertSuccess();
            result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: $"No delta file was specified. Please pass --deltaFileName MyPackage.1.0.0.0_to_1.0.0.1.octodelta");
        }

        [Test]
        public void ShouldWriteErrorWhenNoNewFileIsSpecified()
        {
            var result = ApplyDelta("Basis", "Hash", "Delta", "");

            result.AssertSuccess();
            result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: "No new file name was specified. Please pass --newFileName MyPackage.1.0.0.1.nupkg");
        }

        [Test]
        public void ShouldWriteErrorWhenBasisFileCannotBeFound()
        {
            var basisFile = Path.Combine(DownloadPath, "MyPackage.1.0.0.nupkg");
            var result = ApplyDelta(basisFile, "Hash", "Delta", "New");

            result.AssertSuccess();
            result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: "Could not find basis file: " + basisFile);
        }

        [Test]
        public void ShouldWriteErrorWhenDeltaFileCannotBeFound()
        {
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0")))
            {
                var deltaFilePath = Path.Combine(DownloadPath, "Acme.Web.1.0.0_to_1.0.1.octodelta");
                var result = ApplyDelta(basisFile.FilePath, basisFile.Hash, deltaFilePath, "New");

                result.AssertSuccess();
                result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: "Could not find delta file: " + deltaFilePath);
            }
        }

        [Test]
        public void ShouldWriteErrorWhenBasisFileHashDoesNotMatchSpecifiedFileHash()
        {
            var otherBasisFileHash = "2e9407c9eae20ffa94bf050283f9b4292a48504c";
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0")))
            {
                var deltaFilePath = Path.Combine(DownloadPath,
                    "Acme.Web.1.0.0_to_1.0.1.octodelta");
                using (var deltaFile = File.CreateText(deltaFilePath))
                {
                    deltaFile.WriteLine("This is a delta file!");
                    deltaFile.Flush();
                }

                var result = ApplyDelta(basisFile.FilePath, otherBasisFileHash, deltaFilePath, NewFileName, messageOnError: true);

                result.AssertSuccess();
                result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: $"Basis file hash `{basisFile.Hash}` does not match the file hash specified `{otherBasisFileHash}`");
            }
        }

        [Test]
        public void ShouldWriteErrorWhenDeltaFileIsInvalid()
        {
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0")))
            {
                var deltaFilePath = Path.Combine(DownloadPath,
                    "Acme.Web.1.0.0_to_1.0.1.octodelta");
                using (var deltaFile = File.CreateText(deltaFilePath))
                {
                    deltaFile.WriteLine("This is a delta file!");
                    deltaFile.Flush();
                }

                var result = ApplyDelta(basisFile.FilePath, basisFile.Hash, deltaFilePath, NewFileName);

                result.AssertSuccess();
                result.AssertOutputMatches(
                    $".*\nApplying delta to {Regex.Escape(basisFile.FilePath)} with hash {basisFile.Hash} and storing as {Regex.Escape(DownloadPath)}.*");
                result.AssertOutput("The delta file appears to be corrupt.");
                result.AssertServiceMessage(ServiceMessageNames.PackageDeltaVerification.Name, message: "The following command: OctoDiff\nFailed with exit code: 2\n");
            }
        }
    }
}
