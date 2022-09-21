using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Commands
{
    [Command("apply-delta", Description = "Applies a delta file to a package to create a new version of the package")]
    public class ApplyDeltaCommand : Command
    {
        string? basisFileName;
        string? fileHash;
        string? deltaFileName;
        string? newFileName;
        bool showProgress;
        bool skipVerification;

        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;

        public ApplyDeltaCommand(ILog log, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
        {
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.log = log;
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

            try
            {
                ValidateParameters(out basisFilePath, out deltaFilePath, out newFilePath);

                var tempNewFilePath = newFilePath + ".partial";
#if USE_OCTODIFF_EXE
                var factory = new OctoDiffCommandLineRunner(commandLineRunner);
#else
                var factory = new OctoDiffLibraryCallRunner();
#endif
                var octoDiff = factory.OctoDiff
                    .Action("patch")
                    .PositionalArgument(basisFilePath)
                    .PositionalArgument(deltaFilePath)
                    .PositionalArgument(tempNewFilePath);

                if(skipVerification)
                    octoDiff.Flag("skip-verification");
            
                if(showProgress)
                    octoDiff.Flag("progress");

                log.InfoFormat("Applying delta to {0} with hash {1} and storing as {2}", basisFilePath, fileHash ?? string.Empty, newFilePath);

                var result = factory.Execute();
                if (result.ExitCode != 0)
                {
                    fileSystem.DeleteFile(tempNewFilePath, FailureOptions.ThrowOnFailure);
                    throw new CommandLineException("OctoDiff", result.ExitCode, result.Errors);
                }

                fileSystem.MoveFile(tempNewFilePath, newFilePath);

                if (!File.Exists(newFilePath))
                    throw new CommandException($"Failed to apply delta file {deltaFilePath} to {basisFilePath}");
            }
            catch (Exception e) when (e is CommandLineException || e is CommandException)
            {
                log.DeltaVerificationError(e.Message);
                return 0;
            }

            var package = PackagePhysicalFileMetadata.Build(newFilePath);
            if (package == null)
                return 0;

            log.DeltaVerification(newFilePath, package.Hash, package.Size);
            return 0;
        }

        void ValidateParameters(out string basisFilePath, out string deltaFilePath, out string newFilePath)
        {
            Guard.NotNullOrWhiteSpace(basisFileName, "No basis file was specified. Please pass --basisFileName MyPackage.1.0.0.0.nupkg");
            Guard.NotNullOrWhiteSpace(fileHash, "No file hash was specified. Please pass --fileHash MyFileHash");
            Guard.NotNullOrWhiteSpace(deltaFileName, "No delta file was specified. Please pass --deltaFileName MyPackage.1.0.0.0_to_1.0.0.1.octodelta");
            Guard.NotNullOrWhiteSpace(newFileName, "No new file name was specified. Please pass --newFileName MyPackage.1.0.0.1.nupkg");

            basisFilePath = basisFileName;
            if (!File.Exists(basisFileName))
            {
                basisFilePath = Path.GetFullPath(basisFileName);
                if (!File.Exists(basisFilePath)) throw new CommandException("Could not find basis file: " + basisFileName);
            }

            deltaFilePath = deltaFileName;
            if (!File.Exists(deltaFileName))
            {
                deltaFilePath = Path.GetFullPath(deltaFileName);
                if (!File.Exists(deltaFilePath)) throw new CommandException("Could not find delta file: " + deltaFileName);
            }

            // Probably dont need to do this since the server appends a guid in the name... maybe it was originall put here in the name of safety?
            var newPackageDetails = PackageName.FromFile(newFileName);
            newFilePath = Path.Combine(PackageStore.GetPackagesDirectory(), PackageName.ToCachedFileName(newPackageDetails.PackageId, newPackageDetails.Version, newPackageDetails.Extension));
            var hash = HashCalculator.Hash(basisFileName);
            if (hash != fileHash)
            {
                throw new CommandException($"Basis file hash `{hash}` does not match the file hash specified `{fileHash}`");
            }
        }
    }
}