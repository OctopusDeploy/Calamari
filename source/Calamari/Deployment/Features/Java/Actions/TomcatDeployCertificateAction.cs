using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Certificates.Java.Tomcat;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class TomcatDeployCertificateAction : JavaAction
    {
        readonly ILog log;

        public TomcatDeployCertificateAction(JavaRunner runner, ILog log): base(runner)
        {
            this.log = log;
        }

        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var tomcatVersion = GetTomcatVersion(variables);
            log.Info("Deploying certificate to Tomcat");

            if (OctopusFeatureToggles.TomcatNativeIntegrationFeatureToggle.IsEnabled(variables))
            {
                ExecuteNative(variables, tomcatVersion);
                return;
            }

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

        void ExecuteNative(IVariables variables, string tomcatVersionOutput)
        {
            var certificateId = variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable);

            var catalinaBase = variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.CatalinaBase);
            if (string.IsNullOrWhiteSpace(catalinaBase))
                catalinaBase = variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.CatalinaHome);

            var options = new TomcatCertificateOptions(
                                                        catalinaBase,
                                                        variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Service),
                                                        variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Port),
                                                        ParseImplementation(variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Implementation)),
                                                        TomcatVersion.Parse(tomcatVersionOutput),
                                                        variables.GetFlag(SpecialVariables.Action.Java.TomcatDeployCertificate.Default, true),
                                                        variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.Hostname),
                                                        variables.Get(certificateId + ".PrivateKeyPem"),
                                                        variables.Get(certificateId + ".CertificatePem"),
                                                        variables.Get(SpecialVariables.Action.Java.JavaKeystore.Password),
                                                        variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreAlias),
                                                        variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreFilename),
                                                        variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.PrivateKeyFilename),
                                                        variables.Get(SpecialVariables.Action.Java.TomcatDeployCertificate.PublicKeyFilename));

            new TomcatConnectorConfigurator(log).ConfigureHttps(options);
        }

        static TomcatHttpsImplementation ParseImplementation(string value)
        {
            switch ((value ?? "NIO").Trim().ToUpperInvariant())
            {
                case "APR":
                    return TomcatHttpsImplementation.Apr;
                case "NIO2":
                    return TomcatHttpsImplementation.Nio2;
                case "BIO":
                    return TomcatHttpsImplementation.Bio;
                default:
                    return TomcatHttpsImplementation.Nio;
            }
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
                    log.Verbose(stdOut);
                    version.AppendLine(stdOut);
                },
                log.Error);

            if (versionCheck.ExitCode != 0)
            {
                throw new CommandException($"Attempt to obtain tomcat version failed with exit code {versionCheck.ExitCode}.");
            }
            return version.ToString();
        }
    }
}
