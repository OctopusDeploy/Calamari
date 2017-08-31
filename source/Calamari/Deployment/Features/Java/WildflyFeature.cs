using System;
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

            if (!variables.GetFlag(SpecialVariables.Action.Java.Wildfly.Feature))
                return;

            // Environment variables are used to pass parameters to the Java library
            SetEnvironmentVariable("OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", 
                deployment.Variables.Get(
                    SpecialVariables.Package.Output.InstallationPackagePath, deployment.PackageFilePath));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Name", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.DeployName));             
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Controller", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Controller));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_User", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.User));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Password", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Password));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Enabled", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Enabled));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Port", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Port));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Protocol", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Protocol));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_EnabledServerGroup", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.EnabledServerGroup));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_DisabledServerGroup", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.DisabledServerGroup));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_ServerType", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.ServerType));

            Log.Verbose("Invoking java.exe to perform WildFly integration");
            runJava("com.octopus.calamari.wildfly.WildflyDeploy");
        }

        static void SetEnvironmentVariable(string name, string value)
        {
            Log.Verbose($"Setting environment variable: {name} = '{value}'");
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}