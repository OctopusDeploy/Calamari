using System;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    public class VhdBuilder
    {
        public static string BuildSampleVhd(string name, bool twoPartitions = false)
        {
            var packageDirectory = TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", name);
            Assert.That(Directory.Exists(packageDirectory), string.Format("Package {0} is not available (expected at {1}).", name, packageDirectory));

            var output = GetTemporaryDirectory(); //create a new temp dir because later we'll zip it up and we want it to be empty
            var vhdPath = Path.Combine(output, name + ".vhdx");
            if (File.Exists(vhdPath))
                File.Delete(vhdPath);

            using (var scriptFile = new TemporaryFile(Path.GetTempFileName()))
            {
                // create an uninistialized VHD with diskpart
                // can't use New-VHD cmdlet as it requires the Hyper-V service which
                // won't run in EC2
                File.WriteAllText(scriptFile.FilePath, CreateVhdDiskPartScrtipt(vhdPath));
                var silentProcessResult = SilentProcessRunner.ExecuteCommand("diskpart", $"/s {scriptFile.FilePath}", output, Console.WriteLine, Console.Error.WriteLine);
                silentProcessResult.ExitCode.Should().Be(0);
            }

            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, InitializeAndCopyFilesScript(vhdPath, packageDirectory, twoPartitions));
                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath, new CalamariVariables());
                result.AssertSuccess();
            }

            return vhdPath;
        }

        private static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private static CalamariResult ExecuteScript(IScriptExecutor psse, string scriptName, IVariables variables)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, variables);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, runner.Output);
        }

        private static string CreateVhdDiskPartScrtipt(string path)
        {
            return $"create vdisk file={path} type=fixed maximum=20";
        }

        private static string InitializeAndCopyFilesScript(string vhdpath, string includefolder, bool twoPartitions)
        {
            var script = $@"
                Function CopyFilesToPartition($partition){{
                    Format-Volume -Partition $partition -Confirm:$false -FileSystem NTFS -force
                    Add-PartitionAccessPath -InputObject $partition -AssignDriveLetter
                    $volume = Get-Volume -Partition $partition
                    $drive = $volume.DriveLetter

                    $drive = $drive + "":\""
                    Write-Host ""Copying from {includefolder} to $drive""
                    Copy-Item -Path {includefolder}\InVhd\* -Destination $drive -Recurse                    
                }}

                Mount-DiskImage -ImagePath {vhdpath} -NoDriveLetter

                $diskImage = Get-DiskImage -ImagePath {vhdpath}
                Initialize-Disk -Number $diskImage.Number -PartitionStyle MBR

                $partition = New-Partition -DiskNumber $diskImage.Number -Size 10MB -AssignDriveLetter:$false -MbrType IFS
                CopyFilesToPartition $partition 
            ";

            if (twoPartitions)
            {
                script += @"
                    $partition = New-Partition -DiskNumber $diskImage.Number -UseMaximumSize -AssignDriveLetter:$false -MbrType IFS
                    CopyFilesToPartition $partition
                ";
            }

            script += $@"
                Dismount-DiskImage -ImagePath {vhdpath}
                $zipDestination = Split-Path {vhdpath}
                Copy-Item -Path {includefolder}\InZip\* -Destination $zipDestination -Recurse
            ";

            return script;
        }
    }
}