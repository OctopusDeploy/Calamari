using System;
using Calamari.Deployment;
using Calamari.Deployment.Features;
using Calamari.Integration.Processes;

namespace Calamari.Java.Deployment.Features
{
    public class TomcatFeature : IFeature
    {
        private readonly ICommandLineRunner commandLineRunner;

        public TomcatFeature(ICommandLineRunner commandLineRunner)
        {
            this.commandLineRunner = commandLineRunner;
        }

        public string Name => "Octopus.Features.Tomcat";

        public string DeploymentStage => DeploymentStages.BeforeDeploy; 

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            if (!variables.GetFlag(SpecialVariables.Action.Java.Tomcat.Feature))
                return;

            // Environment variables are used to pass parameters to the Java library
            SetEnvironmentVariable("OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", deployment.PackageFilePath);
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Name", variables.Get(SpecialVariables.Action.Java.Tomcat.DeployName));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Version", variables.Get(SpecialVariables.Package.NuGetPackageVersion)); 
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Controller", variables.Get(SpecialVariables.Action.Java.Tomcat.Controller));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_User", variables.Get(SpecialVariables.Action.Java.Tomcat.User));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Password", variables.Get(SpecialVariables.Action.Java.Tomcat.Password));

            Log.Verbose("Invoking java.exe to perform Tomcat integration");
            var result = commandLineRunner.Execute(new CommandLineInvocation("java", "-cp calamari.jar com.octopus.calamari.tomcat.TomcatDeploy"));
            result.VerifySuccess();
        }

        static void SetEnvironmentVariable(string name, string value)
        {
            Log.Verbose($"Setting environment variable: {name} = '{value}'");
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}