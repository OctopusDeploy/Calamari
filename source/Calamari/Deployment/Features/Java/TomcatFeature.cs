using System;
using Calamari.Integration.Processes;

namespace Calamari.Deployment.Features.Java
{
    public class TomcatFeature : JavaBaseFeature, IFeature
    {
        public TomcatFeature(ICommandLineRunner commandLineRunner)
            : base(commandLineRunner)
        {
        }

        public string Name => SpecialVariables.Action.Java.Tomcat.Feature;

        public string DeploymentStage => DeploymentStages.BeforeDeploy; 

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            // Octopus.Features.TomcatDeployManager was set to True previously,
            // but now we rely on the feature being enabled
            if (!(variables.GetFlag(SpecialVariables.Action.Java.Tomcat.Feature) ||
                  (variables.Get(SpecialVariables.Package.EnabledFeatures) ?? "").Contains(SpecialVariables.Action.Java.Tomcat.Feature)))
                return;

            // Environment variables are used to pass parameters to the Java library
            SetEnvironmentVariable("OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", 
                deployment.Variables.Get(SpecialVariables.Package.Output.InstallationPackagePath, deployment.PackageFilePath));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Name", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.DeployName));             
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Controller", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Controller));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_User", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.User));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Password", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Password));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Enabled", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Enabled));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Version",
                variables.Get(SpecialVariables.Action.Java.Tomcat.Version));

            Log.Verbose("Invoking java.exe to perform Tomcat integration");
            runJava("com.octopus.calamari.tomcat.TomcatDeploy");
        }

        static void SetEnvironmentVariable(string name, string value)
        {
            Log.Verbose($"Setting environment variable: {name} = '{value}'");
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}