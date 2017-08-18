using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Features;
using Calamari.Integration.Processes;

namespace Calamari.Java.Deployment.Features
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

            if (!variables.GetFlag(SpecialVariables.Action.Java.Tomcat.Feature))
                return;

            // Environment variables are used to pass parameters to the Java library
            SetEnvironmentVariable("OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", 
                deployment.PackageFilePath);
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Name", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.DeployName));             
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Controller", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Controller));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_User", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.User));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Password", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Password));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Debug", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Debug));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Enabled", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Enabled));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Context", 
                variables.Get(SpecialVariables.Action.Java.Tomcat.Context));
            
            /*
                Versions can either be disabled, use the package version, or use a custom 
                version number.
            */
            var versionType = variables.Get(SpecialVariables.Action.Java.Tomcat.Version)?.ToLower();
            if (SpecialVariables.Action.Java.Tomcat.PackageVersionValue.ToLower().Equals(versionType))
            {
                SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Version",
                    variables.Get(SpecialVariables.Package.NuGetPackageVersion));
            } 
            else if (SpecialVariables.Action.Java.Tomcat.CustomVersionValue.ToLower().Equals(versionType))
            {
                SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Version",
                    variables.Get(SpecialVariables.Action.Java.Tomcat.CustomVersion));
            }
            else
            {
                SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Version", "");
            }

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