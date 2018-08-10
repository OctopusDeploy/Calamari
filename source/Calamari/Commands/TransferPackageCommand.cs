using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Shared;
using Calamari.Shared.Commands;

namespace Calamari.Commands
{
    [Command("transfer-package", Description = "Copies a deployment package to a specific directory")]
    public class TransferPackageCommand : Command, Shared.Commands.ICustomCommand
    {
        public override int Execute(string[] commandLineArguments)
        {
            return 0;
        }

        public ICommandBuilder Run(ICommandBuilder commandBuilder)
        {
            return commandBuilder.AddConvention<TransferPackageConvention>();
        }
    }
}