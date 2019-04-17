using System;
using System.Collections.Generic;
using Calamari.Integration.Processes;

namespace Calamari.Deployment.Features.Java
{
    public class WildflyFeature : JavaBaseFeature, IFeature
    {
        public WildflyFeature(ICommandLineRunner commandLineRunner)
            : base(commandLineRunner)
        {

        }

        public string Name => SpecialVariables.Action.Java.Wildfly.Feature;

        public string DeploymentStage => DeploymentStages.BeforeDeploy; 

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            // Octopus.Features.WildflyDeployCLI was set to True previously,
            // but now we rely on the feature being enabled
            if (!(variables.GetFlag(SpecialVariables.Action.Java.Wildfly.Feature) ||
                  (variables.Get(SpecialVariables.Package.EnabledFeatures) ?? "").Contains(SpecialVariables.Action.Java.Wildfly.Feature)))
                return;

            // Environment variables are used to pass parameters to the Java library
            Log.Verbose("Invoking java.exe to perform WildFly integration");
            runJava("com.octopus.calamari.wildfly.WildflyDeploy", new Dictionary<string, string>()
            {
                {"OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", deployment.Variables.Get(SpecialVariables.Package.Output.InstallationPackagePath, deployment.PackageFilePath)},
                {"OctopusEnvironment_WildFly_Deploy_Name", variables.Get(SpecialVariables.Action.Java.Wildfly.Controller)},
                {"OctopusEnvironment_WildFly_Deploy_User", variables.Get(SpecialVariables.Action.Java.Wildfly.User)},
                {"OctopusEnvironment_WildFly_Deploy_Password", variables.Get(SpecialVariables.Action.Java.Wildfly.Password)},
                {"OctopusEnvironment_WildFly_Deploy_Enabled", variables.Get(SpecialVariables.Action.Java.Wildfly.Enabled)},
                {"OctopusEnvironment_WildFly_Deploy_Port", variables.Get(SpecialVariables.Action.Java.Wildfly.Port)},
                {"OctopusEnvironment_WildFly_Deploy_Protocol", variables.Get(SpecialVariables.Action.Java.Wildfly.Protocol)},
                {"OctopusEnvironment_WildFly_Deploy_EnabledServerGroup", variables.Get(SpecialVariables.Action.Java.Wildfly.EnabledServerGroup)},
                {"OctopusEnvironment_WildFly_Deploy_DisabledServerGroup", variables.Get(SpecialVariables.Action.Java.Wildfly.DisabledServerGroup)},
                {"OctopusEnvironment_WildFly_Deploy_ServerType", variables.Get(SpecialVariables.Action.Java.Wildfly.ServerType)}
            });
        }
    }
}