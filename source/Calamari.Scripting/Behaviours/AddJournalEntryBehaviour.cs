using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Scripting;

namespace Calamari.Commands
{
    class AddJournalEntryBehaviour : IOnFinishBehaviour
    {
        readonly IDeploymentJournalWriter deploymentJournalWriter;

        public AddJournalEntryBehaviour(IDeploymentJournalWriter deploymentJournalWriter)
        {
            this.deploymentJournalWriter = deploymentJournalWriter;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var exitCode = context.Variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
            deploymentJournalWriter.AddJournalEntry(context, exitCode == 0, context.PackageFilePath);
            return this.CompletedTask();
        }
    }
}