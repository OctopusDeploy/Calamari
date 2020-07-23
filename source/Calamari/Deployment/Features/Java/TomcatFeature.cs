using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Features.Java
{
    public class TomcatFeature : IFeature
    {
        readonly JavaRunner javaRunner;

        public TomcatFeature(JavaRunner javaRunner)
        {
            this.javaRunner = javaRunner;
        }

        public string Name => SpecialVariables.Action.Java.Tomcat.Feature;

        public string DeploymentStage => DeploymentStages.BeforeDeploy;

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            // Octopus.Features.TomcatDeployManager was set to True previously,
            // but now we rely on the feature being enabled
            if (!(variables.GetFlag(SpecialVariables.Action.Java.Tomcat.Feature) ||
                  (variables.Get(KnownVariables.Package.EnabledFeatures) ?? "").Contains(SpecialVariables.Action.Java.Tomcat.Feature)))
                return;

            // Environment variables are used to pass parameters to the Java library
            Log.Verbose("Invoking java to perform Tomcat integration");
            javaRunner.Run("com.octopus.calamari.tomcat.TomcatDeploy", new Dictionary<string, string>()
            {
                {"OctopusEnvironment_Octopus_Tentacle_CurrentDeployment_PackageFilePath", deployment.Variables.Get(PackageVariables.Output.InstallationPackagePath, deployment.PackageFilePath)},
                {"OctopusEnvironment_Tomcat_Deploy_Name", variables.Get(SpecialVariables.Action.Java.Tomcat.DeployName)},
                {"OctopusEnvironment_Tomcat_Deploy_Controller", variables.Get(SpecialVariables.Action.Java.Tomcat.Controller)},
                {"OctopusEnvironment_Tomcat_Deploy_User", variables.Get(SpecialVariables.Action.Java.Tomcat.User)},
                {"OctopusEnvironment_Tomcat_Deploy_Password", variables.Get(SpecialVariables.Action.Java.Tomcat.Password)},
                {"OctopusEnvironment_Tomcat_Deploy_Enabled", variables.Get(SpecialVariables.Action.Java.Tomcat.Enabled)},
                {"OctopusEnvironment_Tomcat_Deploy_Version", variables.Get(SpecialVariables.Action.Java.Tomcat.Version)},
            });
        }
    }
}