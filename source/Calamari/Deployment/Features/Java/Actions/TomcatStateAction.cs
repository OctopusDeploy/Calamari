using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class TomcatStateAction : JavaAction
    {
        readonly ILog log;

        public TomcatStateAction(JavaRunner runner, ILog log): base(runner)
        {
            this.log = log;
        }


        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            log.Info("Updating Tomcat state");
            runner.Run("com.octopus.calamari.tomcat.TomcatState", new Dictionary<string, string>()
            {
                {"OctopusEnvironment_Tomcat_Deploy_Name", variables.Get(SpecialVariables.Action.Java.Tomcat.DeployName)},
                {"OctopusEnvironment_Tomcat_Deploy_Version", variables.Get(SpecialVariables.Action.Java.Tomcat.Version)},
                {"OctopusEnvironment_Tomcat_Deploy_Controller", variables.Get(SpecialVariables.Action.Java.Tomcat.Controller)},
                {"OctopusEnvironment_Tomcat_Deploy_User", variables.Get(SpecialVariables.Action.Java.Tomcat.User)},
                {"OctopusEnvironment_Tomcat_Deploy_Password", variables.Get(SpecialVariables.Action.Java.Tomcat.Password)},
                {"OctopusEnvironment_Tomcat_Deploy_Enabled", variables.Get(SpecialVariables.Action.Java.Tomcat.Enabled)},
            });
        }
    }
}