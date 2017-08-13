using System;
using System.IO;
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

        public string Name => SpecialVariables.Action.Java.Tomcat.Feature;

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
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Debug", variables.Get(SpecialVariables.Action.Java.Tomcat.Debug));
            SetEnvironmentVariable("OctopusEnvironment_Tomcat_Deploy_Enabled", variables.Get(SpecialVariables.Action.Java.Tomcat.Enabled));

            Log.Verbose("Invoking java.exe to perform Tomcat integration");
            /*
                The precondition script will set the OctopusEnvironment_Java_Bin environment variable based
                on where it found the java executable based on the JAVA_HOME environment
                variable. If OctopusEnvironment_Java_Bin is empty or null, it means that the precondition
                found java on the path.
            */
            var javaBin = Environment.GetEnvironmentVariable("OctopusEnvironment_Java_Bin") ?? "";
            /*
                The calamari.jar file must be next to the Calamari.Java executable. This
                is the path of the entry point of the application (i.e. Calamari.Java.exe)
            */
            var calamariDir = AppDomain.CurrentDomain.BaseDirectory;
            var result = commandLineRunner.Execute(new CommandLineInvocation(
                $"{javaBin.Trim().Trim()}java", 
                "-cp " + calamariDir + "calamari.jar com.octopus.calamari.tomcat.TomcatDeploy",
                calamariDir));
            result.VerifySuccess();
        }

        static void SetEnvironmentVariable(string name, string value)
        {
            Log.Verbose($"Setting environment variable: {name} = '{value}'");
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}