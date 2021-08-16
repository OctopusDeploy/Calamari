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

        public CommandLockDecorator(ILog ilog, ICommandWithArgs command, IVariables variables, Journal journal)
        {
            this.ilog = ilog;
            this.command = command;
            this.variables = variables;

            var deploymentId = variables.Get(KnownVariables.Deployment.Id);
           // var packageFileName = variables.Get(KnownVariables.Package.)
           //Get some way of uniquely identifying the package/version

        }

        public int Execute(string[] commandLineArguments)
        {
            return command.Execute(commandLineArguments);
        }
    }
}