using System;
using Calamari.Integration.Processes;

namespace Calamari.Java.Deployment.Features
{
    /// <summary>
    /// A base class for features that run Java against the Octopus Deploy 
    /// Java library
    /// </summary>
    public abstract class JavaBaseFeature
    {
        private const string JavaLibraryEnvVar = "CalmariDependencyPathOctopusDependenciesJava";
        private const string JavaBinEnvVar = "OctopusEnvironment_Java_Bin";
        private readonly ICommandLineRunner commandLineRunner;
        
        protected JavaBaseFeature(ICommandLineRunner commandLineRunner)
        {
            this.commandLineRunner = commandLineRunner;
        }
        
        /// <summary>
        /// Execute java running the Octopus Deploy Java library
        /// </summary>
        protected void runJava(string mainClass)
        {           
            /*
                The precondition script will set the OctopusEnvironment_Java_Bin environment variable based
                on where it found the java executable based on the JAVA_HOME environment
                variable. If OctopusEnvironment_Java_Bin is empty or null, it means that the precondition
                found java on the path.
            */
            var javaBin = Environment.GetEnvironmentVariable(JavaBinEnvVar) ?? "";
            /*
                The precondition script will also set the location of the calamari.jar file
            */
            var javaLib = Environment.GetEnvironmentVariable(JavaLibraryEnvVar) ?? "";
            var result = commandLineRunner.Execute(new CommandLineInvocation(
                $"{javaBin.Trim()}java", 
                $"-cp calamari.jar {mainClass}",
                $"{javaLib}contentFiles\\any\\any"));
            result.VerifySuccess();
        }
    }
}