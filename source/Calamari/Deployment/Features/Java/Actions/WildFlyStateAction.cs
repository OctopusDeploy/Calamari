using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class WildFlyStateAction : JavaAction
    {
        public WildFlyStateAction(JavaRunner runner): base(runner)
        {
        }

        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            Log.Info("Updating WildFly state");
            runner.Run("com.octopus.calamari.wildfly.WildflyState", new Dictionary<string, string>()
            {
                {"OctopusEnvironment_WildFly_Deploy_Name", variables.Get(SpecialVariables.Action.Java.WildFly.DeployName)},
                {"OctopusEnvironment_WildFly_Deploy_Controller", variables.Get(SpecialVariables.Action.Java.WildFly.Controller)},
                {"OctopusEnvironment_WildFly_Deploy_Port", variables.Get(SpecialVariables.Action.Java.WildFly.Port)},
                {"OctopusEnvironment_WildFly_Deploy_Protocol", variables.Get(SpecialVariables.Action.Java.WildFly.Protocol)},
                {"OctopusEnvironment_WildFly_Deploy_User", variables.Get(SpecialVariables.Action.Java.WildFly.User)},
                {"OctopusEnvironment_WildFly_Deploy_Password", variables.Get(SpecialVariables.Action.Java.WildFly.Password)},
                {"OctopusEnvironment_WildFly_Deploy_Enabled", variables.Get(SpecialVariables.Action.Java.WildFly.Enabled)},
                {"OctopusEnvironment_WildFly_Deploy_EnabledServerGroup", variables.Get(SpecialVariables.Action.Java.WildFly.EnabledServerGroup)},
                {"OctopusEnvironment_WildFly_Deploy_DisabledServerGroup", variables.Get(SpecialVariables.Action.Java.WildFly.DisabledServerGroup)},
                {"OctopusEnvironment_WildFly_Deploy_ServerType", variables.Get(SpecialVariables.Action.Java.WildFly.ServerType)},
            });
        }
    }
}