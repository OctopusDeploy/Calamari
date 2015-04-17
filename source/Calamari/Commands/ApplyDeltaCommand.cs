using System;
using System.IO;
using System.Net.Configuration;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;

namespace Calamari.Commands
{
    [Command("apply-delta", Description = "Applies a delta file to a package to create a new version of the package")]
    public class ApplyDeltaCommand : Command
    {
        string basisFileName;
        string fileHash;
        string deltaFileName;
        string newFileName;
        bool showProgress;
        bool skipVerification;
        readonly PackageStore packageStore = new PackageStore();
        readonly ICalamariFileSystem fileSystem = new CalamariPhysicalFileSystem();
        readonly ISemaphore semaphore = new SystemSemaphore();

        public ApplyDeltaCommand()
        {
            Options.Add("basisFileName=", "The file that the delta was created for.", v => basisFileName = v);
            Options.Add("fileHash=", "", v => fileHash = v);
            Options.Add("deltaFileName=", "The delta to apply to the basis file", v => deltaFileName = v);
            Options.Add("newFileName=", "The file to write the result to.", v => newFileName = v);
            Options.Add("progress", "Whether progress should be written to stdout", v => showProgress = true);
            Options.Add("skipVerification",
                "Skip checking whether the basis file is the same as the file used to produce the signature that created the delta.",
                v => skipVerification = true);
        }
        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            string deltaFilePath;
            string newFilePath;
            string basisFilePath;
            ValidateParameters(out basisFilePath, out deltaFilePath, out newFilePath);
            fileSystem.EnsureDiskHasEnoughFreeSpace(packageStore.GetPackagesDirectory());

            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(),
                new ServiceMessageCommandOutput(new VariableDictionary())));

            var tempNewFilePath = newFilePath + ".partial";
            var executable = FindOctoDiffExecutable();
            var octoDiff = CommandLine.Execute(executable)
                .Action("patch")
                .PositionalArgument(basisFilePath)
                .PositionalArgument(deltaFilePath)
                .PositionalArgument(tempNewFilePath);

            if(skipVerification)
                octoDiff.Flag("skip-verification");
            
            if(showProgress)
                octoDiff.Flag("progress");

            Log.Info("Applying delta to {0} with hash {1} and storing as {2}", basisFilePath, fileHash,
                newFilePath);

            var result = commandLineRunner.Execute(octoDiff.Build());
            if (result.ExitCode != 0)
            {
                fileSystem.DeleteFile(tempNewFilePath, DeletionOptions.TryThreeTimes);
                throw new CommandLineException(executable, result.ExitCode, result.Errors);
            }

            File.Move(tempNewFilePath, newFilePath);

            if (!File.Exists(newFilePath))
                throw new CommandException("Failed to apply delta file " + deltaFilePath + " to " +
                                           basisFilePath);

            var package = packageStore.GetPackage(newFilePath);
            if (package == null) return 0;

            using (var file = new FileStream(package.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var size = file.Length;
                Log.ServiceMessages.DeltaVerification(package.FullPath, package.Metadata.Hash, size);
            }

            return 0;
        }

        void ValidateParameters(out string basisFilePath, out string deltaFilePath, out string newFilePath)
        {
            Guard.NotNullOrWhiteSpace(basisFileName, "No basis file was specified. Please pass --basisFileName MyPackage.1.0.0.0.nupkg");
            Guard.NotNullOrWhiteSpace(fileHash, "No file hash was specified. Please pass --fileHash MyFileHash");
            Guard.NotNullOrWhiteSpace(deltaFileName, "No delta file was specified. Please pass --deltaFileName MyPackage.1.0.0.0_to_1.0.0.1.octodelta");
            Guard.NotNullOrWhiteSpace(newFileName, "No new file name was specified. Please pass --newFileName MyPackage.1.0.0.1.nupkg");

            basisFilePath = Path.GetFullPath(basisFileName);
            deltaFilePath = Path.GetFullPath(deltaFileName);
            newFilePath = Path.Combine(packageStore.GetPackagesDirectory(), newFileName + "-" + Guid.NewGuid());
            if (!File.Exists(basisFilePath)) throw new CommandException("Could not find basis file: " + basisFileName);
            if (!File.Exists(deltaFilePath)) throw new CommandException("Could not find delta file: " + deltaFileName);

            var previousPackage = packageStore.GetPackage(basisFilePath);
            if (previousPackage.Metadata.Hash != fileHash)
            {
                throw new CommandException("Basis file hash " + previousPackage.Metadata.Hash +
                                           " does not match the file hash specified " + fileHash);
            }
        }

        string FindOctoDiffExecutable()
        {
            var basePath = Path.GetDirectoryName(GetType().Assembly.Location);
            var exePath = Path.Combine(basePath, "Octodiff.exe");
            if (!File.Exists(exePath))
                throw new CommandException("Unable to find Octodiff.exe in " + basePath);
            return exePath;
        }
    }
}
