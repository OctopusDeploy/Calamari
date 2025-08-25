using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Commands
{
    [Command(Name, Description = "Write populated templates from a package into one or more git repositories")]
    public class UpdateArgoCDAppImagesCommand : Command
    {
        public const string Name = "update-argo-cd-app-images";
        
        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;

        public UpdateArgoCDAppImagesCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var runningDeployment = new RunningDeployment(null, variables);


            var conventions = new List<IConvention>
            {
                
            };
                
            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);
            
            return 0;
        }
    }
}