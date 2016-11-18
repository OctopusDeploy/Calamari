using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    public class VhdBuilder
    {
        public static string BuildSampleVhd(string name)
        {
            var packageDirectory = TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", name);
            Assert.That(Directory.Exists(packageDirectory), string.Format("Package {0} is not available (expected at {1}).", name, packageDirectory));

            var output = GetTemporaryDirectory(); //create a new temp dir because later we'll zip it up and we want it to be empty
            var path = Path.Combine(output, name + ".vhdx");
            if (File.Exists(path))
                File.Delete(path);

            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, CreateScript(path, packageDirectory));
                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, new CalamariVariableDictionary());
                result.AssertSuccess();
            }

            return path;
        }

        private static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private static CalamariResult ExecuteScript(IScriptEngine psse, string scriptName, CalamariVariableDictionary variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(capture);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, capture);
        }

        private static string CreateScript(string vhdpath, string includefolder)
        {
            return $@"
                $drive = (New-VHD -path {vhdpath} -SizeBytes 10MB -Fixed | `
                    Mount-VHD -Passthru |  `
                    get-disk -number {{$_.DiskNumber}} | `
                    Initialize-Disk -PartitionStyle MBR -PassThru | `
                    New-Partition -UseMaximumSize -AssignDriveLetter:$False -MbrType IFS | `
                    Format-Volume -Confirm:$false -FileSystem NTFS -force | `
                    get-partition | `
                    Add-PartitionAccessPath -AssignDriveLetter -PassThru | `
                    get-volume).DriveLetter 

                $drive = $drive + "":\""
                Write-Host ""Copying from {includefolder} to $drive""
                Copy-Item -Path {includefolder}\InVhd\* -Destination $drive -Recurse
                Dismount-VHD {vhdpath};

                $zipDestination = Split-Path {vhdpath}
                Copy-Item -Path {includefolder}\InZip\* -Destination $zipDestination -Recurse";
        }
    }
}