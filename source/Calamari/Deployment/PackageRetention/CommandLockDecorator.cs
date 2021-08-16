using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Commands.Options;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention
{
    public class CommandLockDecorator : ICommandWithArgs
    {
        readonly ILog ilog;
        readonly ICommandWithArgs command;
        readonly IVariables variables;
        readonly Journal journal;

        DeploymentID DeploymentID => new DeploymentID(variables.Get(KnownVariables.Deployment.Id));
        PackageID PackageID => new PackageID("MyPackage");   //Work out a good way of uniquely identifying the package/version

        public CommandLockDecorator(ILog ilog, ICommandWithArgs command, IVariables variables, Journal journal)
        {
            this.ilog = ilog;
            this.command = command;
            this.variables = variables;

           // var packageFileName = variables.Get(KnownVariables.Package.)

           Log.Verbose($"Decorating {command.GetType().Name} with command lock.");
           //TODO: make journal load from file here? Otherwise look at that occuring in the default constructor
           //Will need to consider file locking/retries etc too
           this.journal = journal;
        }

        public int Execute(string[] commandLineArguments)
        {
            journal.RegisterPackageUse(PackageID, DeploymentID);
            return command.Execute(commandLineArguments);
        }
    }
}