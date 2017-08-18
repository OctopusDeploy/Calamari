using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Features;
using Calamari.Integration.Processes;

namespace Calamari.Java.Deployment.Features
{
    public class WildflyStateFeature : IFeature
    {
        private readonly ICommandLineRunner commandLineRunner;

        public WildflyStateFeature(ICommandLineRunner commandLineRunner)
        {
            this.commandLineRunner = commandLineRunner;
        }

        public string Name => SpecialVariables.Action.Java.Wildfly.StateFeature;

        public string DeploymentStage => DeploymentStages.BeforeDeploy; 

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            if (!variables.GetFlag(SpecialVariables.Action.Java.Wildfly.StateFeature))
                return;

            // Environment variables are used to pass parameters to the Java library
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Name", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.DeployName));             
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Controller", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Controller));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_User", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.User));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Password", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Password));
            SetEnvironmentVariable("OctopusEnvironment_WildFly_Deploy_Debug", 
                variables.Get(SpecialVariables.Action.Java.Wildfly.Debug));
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

            Log.Verbose("Invoking java.exe to perform WildFly integration");
            /*
                The precondition script will set the OctopusEnvironment_Java_Bin environment variable based
                on where it found the java executable based on the JAVA_HOME environment
                variable. If OctopusEnvironment_Java_Bin is empty or null, it means that the precondition
                found java on the path.
            */
            var javaBin = Environment.GetEnvironmentVariable("OctopusEnvironment_Java_Bin") ?? "";
            /*
                The precondition script will also set the location of the calamari.jar file
            */
            var javaLib = Environment.GetEnvironmentVariable("CalmariDependencyPathOctopusDependenciesJava") ?? "";
            var result = commandLineRunner.Execute(new CommandLineInvocation(
                $"{javaBin.Trim()}java", 
                "-cp " + javaLib + "contentFiles\\any\\any\\calamari.jar com.octopus.calamari.wildfly.WildflyState",
                javaLib + "contentFiles\\any\\any"));
            result.VerifySuccess();
        }

        static void SetEnvironmentVariable(string name, string value)
        {
            Log.Verbose($"Setting environment variable: {name} = '{value}'");
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}