using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Features.Java
{
    public class WildflyFeature : IFeature
    {
        readonly JavaRunner javaRunner;

        public WildflyFeature(JavaRunner javaRunner)
        {
            this.javaRunner = javaRunner;
        }

        public string Name => SpecialVariables.Action.Java.WildFly.Feature;

        public string DeploymentStage => DeploymentStages.BeforeDeploy;

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            // Octopus.Features.WildflyDeployCLI was set to True previously,
            // but now we rely on the feature being enabled
            if (!(variables.GetFlag(SpecialVariables.Action.Java.WildFly.Feature) ||
                  (variables.Get(KnownVariables.Package.EnabledFeatures) ?? "").Contains(SpecialVariables.Action.Java.WildFly.Feature)))
                return;

            // Environment variables are used to pass parameters to the Java library
            Log.Verbose("Invoking java to perform WildFly integration");
            javaRunner.Run("com.octopus.calamari.wildfly.WildflyDeploy", new Dictionary<string, string>()
            {
                {"OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", deployment.Variables.Get(PackageVariables.Output.InstallationPackagePath, deployment.PackageFilePath)},
                {"OctopusEnvironment_WildFly_Deploy_Name", variables.Get(SpecialVariables.Action.Java.WildFly.DeployName)},
                {"OctopusEnvironment_WildFly_Deploy_User", variables.Get(SpecialVariables.Action.Java.WildFly.User)},
                {"OctopusEnvironment_WildFly_Deploy_Password", variables.Get(SpecialVariables.Action.Java.WildFly.Password)},
                {"OctopusEnvironment_WildFly_Deploy_Enabled", variables.Get(SpecialVariables.Action.Java.WildFly.Enabled)},
                {"OctopusEnvironment_WildFly_Deploy_Port", variables.Get(SpecialVariables.Action.Java.WildFly.Port)},
                {"OctopusEnvironment_WildFly_Deploy_Protocol", variables.Get(SpecialVariables.Action.Java.WildFly.Protocol)},
                {"OctopusEnvironment_WildFly_Deploy_EnabledServerGroup", variables.Get(SpecialVariables.Action.Java.WildFly.EnabledServerGroup)},
                {"OctopusEnvironment_WildFly_Deploy_DisabledServerGroup", variables.Get(SpecialVariables.Action.Java.WildFly.DisabledServerGroup)},
                {"OctopusEnvironment_WildFly_Deploy_ServerType", variables.Get(SpecialVariables.Action.Java.WildFly.ServerType)},
                {"OctopusEnvironment_WildFly_Deploy_Controller", variables.Get(SpecialVariables.Action.Java.WildFly.Controller)}
            });
        }
    }
}