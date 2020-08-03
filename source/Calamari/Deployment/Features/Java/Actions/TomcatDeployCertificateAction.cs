using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class TomcatDeployCertificateAction : JavaAction
    {
        public TomcatDeployCertificateAction(JavaRunner runner): base(runner)
        {
        }

        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var tomcatVersion = GetTomcatVersion(variables);
            Log.Info("Deploying certificate to Tomcat");
            runner.Run("com.octopus.calamari.tomcathttps.TomcatHttpsConfig", new Dictionary<string, string>()
            {
                {"OctopusEnvironment_Java_Certificate_Variable", variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable)},                
                {"OctopusEnvironment_Java_Certificate_Password", variables.Get(SpecialVariables.Action.Java.JavaKeystore.Password)},                               
                {"OctopusEnvironment_Java_Certificate_KeystoreFilename", variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreFilename)},
                {"OctopusEnvironment_Java_Certificate_KeystoreAlias", variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreAlias)},
                {"OctopusEnvironment_Java_Certificate_Private_Key", variables.Get(variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) + ".PrivateKeyPem")},
                {"OctopusEnvironment_Java_Certificate_Public_Key", variables.Get(variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) + ".CertificatePem")},
                {"OctopusEnvironment_Java_Certificate_Public_Key_Subject", variables.Get(variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) + ".Subject")},
                
                {"OctopusEnvironment_Tomcat_Certificate_Version", tomcatVersion},
                {"OctopusEnvironment_Tomcat_Certificate_Default", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Default)},
                {"OctopusEnvironment_Tomcat_Certificate_Hostname", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Hostname)},
                {"OctopusEnvironment_Tomcat_Certificate_CatalinaHome", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.CatalinaHome)},
                {"OctopusEnvironment_Tomcat_Certificate_CatalinaBase", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.CatalinaBase)},
                {"OctopusEnvironment_Tomcat_Certificate_Port", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Port)},
                {"OctopusEnvironment_Tomcat_Certificate_Service", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Service)},
                {"OctopusEnvironment_Tomcat_Certificate_Implementation", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Implementation)},
                {"OctopusEnvironment_Tomcat_Certificate_PrivateKeyFilename", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.PrivateKeyFilename)},
                {"OctopusEnvironment_Tomcat_Certificate_PublicKeyFilename", variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.PublicKeyFilename)},
            });
        }
        
        string GetTomcatVersion(IVariables variables)
        {
            var catalinaHome = variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.CatalinaHome) ??
                                Environment.GetEnvironmentVariable("CATALINA_HOME");;
            var catalinaPath = Path.Combine(catalinaHome, "lib", "catalina.jar");

            if (!File.Exists(catalinaPath))
            {
                throw new CommandException("TOMCAT-HTTPS-ERROR-0018: " +
                                           $"Failed to find the file {catalinaPath} " +
                                           "http://g.octopushq.com/JavaAppDeploy#tomcat-https-error-0018");
            }

            var version = new StringBuilder();
            var versionCheck = SilentProcessRunner.ExecuteCommand(JavaRuntime.CmdPath,
                $"-cp \"{catalinaPath}\" org.apache.catalina.util.ServerInfo", ".", 
                (stdOut) =>
                {
                    Log.Verbose(stdOut);
                    version.AppendLine(stdOut);
                },
                Console.Error.WriteLine);

            if (versionCheck.ExitCode != 0)
            {
                throw new CommandException($"Attempt to obtain tomcat version failed with exit code {versionCheck.ExitCode}.");
            }
            return version.ToString();
        }
    }
}