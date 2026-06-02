using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class TransferPackageFixture : CalamariFixture
    {
        TemporaryFile nupkgFile;

        [OneTimeSetUp]
        public void Init()
        {
            nupkgFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0"));
        }

        string StagingDirectory { get; set; }
        string CustomDirectory { get; set; }


        [SetUp]
        public void SetUp()
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            // Ensure staging directory exists and is empty
            StagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            CustomDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestCustom");
            fileSystem.EnsureDirectoryExists(StagingDirectory);
            fileSystem.PurgeDirectory(StagingDirectory, FailureOptions.ThrowOnFailure);


            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(StagingDirectory, "DeploymentJournal.xml"));

        }

        [Test]
        public void ShouldPreserveFileInStagingDirectory()
        {
            var result = TransferPackage();

            result.AssertSuccess();
            Assert.IsTrue(File.Exists(nupkgFile.FilePath));
        }

        [Test]
        public void ShouldCopyFileToTransferPath()
        {
            var result = TransferPackage();

            result.AssertSuccess();

            var outputResult = Path.Combine(CustomDirectory, Path.GetFileName(nupkgFile.FilePath));
            Assert.IsTrue(File.Exists(outputResult));
        }

        [Test]
        public void ShouldProvideOutputVaribles()
        {
            var result = TransferPackage();

            result.AssertSuccess();

            var outputResult = Path.Combine(CustomDirectory, Path.GetFileName(nupkgFile.FilePath));

            result.AssertOutputVariable(PackageVariables.Output.DirectoryPath, Is.EqualTo(CustomDirectory));
            result.AssertOutputVariable(PackageVariables.Output.FileName,Is.EqualTo(Path.GetFileName(nupkgFile.FilePath)));
            result.AssertOutputVariable(PackageVariables.Output.FilePath, Is.EqualTo(outputResult));
        }

        [Test]
        public void ShouldOutputMessageToLogs()
        {
            var result = TransferPackage();
            result.AssertSuccess();

            result.AssertOutput(
                $"Copied package '{Path.GetFileName(nupkgFile.FilePath)}' to directory '{CustomDirectory}'");
        }

        [TestCase(".nupkg")]
        [TestCase(".zip")]
        public void ShouldCopyPackageRegardlessOfFormat(string extension)
        {
            using (var packageFile = new TemporaryFile(BuildPackageWithExtension(extension)))
            {
                var result = TransferPackage(packageFile.FilePath);
                result.AssertSuccess();

                var outputResult = Path.Combine(CustomDirectory, Path.GetFileName(packageFile.FilePath));
                Assert.IsTrue(File.Exists(outputResult));
                result.AssertOutputVariable(PackageVariables.Output.FilePath, Is.EqualTo(outputResult));
            }
        }

        static string BuildPackageWithExtension(string extension)
        {
            if (extension == ".nupkg")
                return PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.2");

            var sourceDir = Path.Combine(Path.GetTempPath(), "CalamariTransferZipSource-" + Guid.NewGuid());
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "marker.txt"), "payload");
            return PackageBuilder.BuildSimpleZip("Acme.Raw", "1.0.0", sourceDir);
        }

        CalamariResult TransferPackage() => TransferPackage(nupkgFile.FilePath);

        CalamariResult TransferPackage(string packageFilePath)
        {
            var variables = new VariableDictionary
            {
                [PackageVariables.TransferPath] = CustomDirectory,
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
    }
}
