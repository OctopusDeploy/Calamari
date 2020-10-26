using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Retention;
using Calamari.Integration.Time;

namespace Calamari.Commands
{
    [Command("clean", Description = "Removes packages and files according to the configured retention policy")]
    public class CleanCommand : Command
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;

        string retentionPolicySet;
        int days;
        int releases;

        public CleanCommand(IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            Options.Add("retentionPolicySet=", "The release-policy-set ID", x => retentionPolicySet = x);
            Options.Add("days=", "Number of days to keep artifacts", x => int.TryParse(x, out days));
            //TODO: rename 'releases' to 'deployments' here 
            Options.Add("releases=", "Number of releases to keep artifacts for", x => int.TryParse(x, out releases));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(retentionPolicySet, "No retention-policy-set was specified. Please pass --retentionPolicySet \"Environments-2/projects-161/Step-Package B/machines-65/<default>\"");

            if (days <=0 && releases <= 0)
                throw new CommandException("A value must be provided for either --days or --releases");

            var deploymentJournal = new DeploymentJournal(fileSystem, SemaphoreFactory.Get(), variables);
            var clock = new SystemClock();

            var retentionPolicy = new RetentionPolicy(fileSystem, deploymentJournal, clock);
            retentionPolicy.ApplyRetentionPolicy(retentionPolicySet, days, releases);

            return 0;
        }
    }
}