using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace Calamari.Commands
{
    [Command("apply-delta", Description = "Applies a delta file to a package to create a new version of the package")]
    public class ApplyDeltaCommand : Command
    {
        string basisFileName;
        string fileHash;
        string deltaFileName;
        string newFileName;
        readonly PackageStore packageStore = new PackageStore();
        IProgressReporter progressReporter;
        string feedId;

        public ApplyDeltaCommand()
        {
            Options.Add("basisFileName=", "", v => basisFileName = v);
            Options.Add("fileHash=", "", v => fileHash = v);
            Options.Add("deltaFileName=", "", v => deltaFileName = v);
            Options.Add("newFileName=", "", v => newFileName = v);
            Options.Add("progress", "", v => progressReporter = new ConsoleProgressReporter());
        }
        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            string deltaFilePath;
            string newFilePath;
            string basisFilePath;
            ValidateParameters(out basisFilePath, out deltaFilePath, out newFilePath);

            var deltaApplier = new DeltaApplier
            {
                SkipHashCheck = true
            };
            using(var basisStream = new FileStream(basisFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var deltaStream = new FileStream(deltaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var newFileStream = new FileStream(newFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                Log.Info("Applying delta to {0} with hash {1} and storing as {2}", basisFilePath, fileHash, newFilePath);
                Log.Verbose("##octopus[stdout-verbose]");
                deltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream, progressReporter), newFileStream);
                Log.Verbose("##octopus[stdout-default]");
            }

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
            if (String.IsNullOrWhiteSpace(basisFileName))
            {
                throw new CommandException("No basis file was specified. Please pass --basisFileName MyPackage.1.0.0.0.nupkg");
            }
            if (String.IsNullOrWhiteSpace(fileHash))
            {
                throw new CommandException("No file hash was specified. Please pass --fileHash MyFileHash");
            }
            if (String.IsNullOrWhiteSpace(deltaFileName))
            {
                throw new CommandException(
                    "No delta file was specified. Please pass --deltaFileName MyPackage.1.0.0.0_to_1.0.0.1.octodelta");
            }
            if (String.IsNullOrWhiteSpace(newFileName))
            {
                throw new CommandException(
                    "No new file name was specified. Please pass --newFileName MyPackage.1.0.0.1.nupkg");
            }

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
    }
}
