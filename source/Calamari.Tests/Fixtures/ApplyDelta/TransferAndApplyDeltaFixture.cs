using System;
using System.IO;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.ApplyDelta
{
    [TestFixture]
    public class TransferAndApplyDeltaFixture : CalamariFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "TransferAndApplyDelta");
        static readonly string DownloadPath = Path.Combine(TentacleHome, "Files");

        string transferDirectory = null!;

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
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            transferDirectory = Path.Combine(Path.GetTempPath(), "CalamariTransferDelta-" + Guid.NewGuid());
            fileSystem.EnsureDirectoryExists(transferDirectory);

            if (!Directory.Exists(DownloadPath))
                Directory.CreateDirectory(DownloadPath);

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(transferDirectory, "DeploymentJournal.xml"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(DownloadPath))
                Directory.Delete(DownloadPath, true);
            if (Directory.Exists(transferDirectory))
                Directory.Delete(transferDirectory, true);
        }

        [Test]
        public void TransferredBasisPackageCanBeReconstructedByApplyDelta()
        {
            using (var basisFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0")))
            {
                var transferResult = TransferPackage(basisFile.FilePath);
                transferResult.AssertSuccess();

                var transferredBasisPath = Path.Combine(transferDirectory, Path.GetFileName(basisFile.FilePath));
                Assert.IsTrue(File.Exists(transferredBasisPath), "Basis package was not transferred to the expected location.");

                using (var signatureFile = new TemporaryFile(transferredBasisPath + ".octosig"))
                {
                    var signatureExitCode = Octodiff.Program.Main(new[] { "signature", transferredBasisPath, signatureFile.FilePath });
                    Assert.That(signatureExitCode, Is.EqualTo(0));

                    using (var newFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.1", true)))
                    using (var deltaFile = new TemporaryFile(transferredBasisPath + "_to_Acme.Web.1.0.1.nupkg.octodelta"))
                    {
                        var deltaExitCode = Octodiff.Program.Main(new[] { "delta", signatureFile.FilePath, newFile.FilePath, deltaFile.FilePath });
                        Assert.That(deltaExitCode, Is.EqualTo(0));

                        var transferredBasisHash = HashCalculator.Hash(transferredBasisPath);
                        var patchResult = ApplyDelta(transferredBasisPath, transferredBasisHash, deltaFile.FilePath, "Acme.Web.1.0.1.nupkg");
                        patchResult.AssertSuccess();

                        patchResult.AssertPackageDeltaVerificationServiceMessage();
                        Assert.AreEqual(newFile.Hash, patchResult.CapturedOutput.DeltaVerification.Hash);
                        Assert.AreEqual(newFile.Hash, HashCalculator.Hash(patchResult.CapturedOutput.DeltaVerification.FullPathOnRemoteMachine));
                    }
                }
            }
        }

        CalamariResult TransferPackage(string packageFilePath)
        {
            var variables = new VariableDictionary
            {
                [PackageVariables.TransferPath] = transferDirectory,
                [PackageVariables.OriginalFileName] = Path.GetFileName(packageFilePath),
                [TentacleVariables.CurrentDeployment.PackageFilePath] = packageFilePath,
                [ActionVariables.Name] = "MyAction",
                [MachineVariables.Name] = "MyMachine"
            };

            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var encryptionKey = variables.SaveAsEncryptedExecutionVariables(variablesFile.FilePath);

                return Invoke(Calamari()
                    .Action("transfer-package")
                    .VariablesFileArguments(variablesFile.FilePath, encryptionKey));
            }
        }

        CalamariResult ApplyDelta(string basisFile, string fileHash, string deltaFile, string newFile)
        {
            return Invoke(Calamari()
                .Action("apply-delta")
                .Argument("basisFileName", basisFile)
                .Argument("fileHash", fileHash)
                .Argument("deltaFileName", deltaFile)
                .Argument("newFileName", newFile));
        }
    }
}
