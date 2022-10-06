using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class WildflyDeployCertificateAction: JavaAction
    {
        public WildflyDeployCertificateAction(JavaRunner runner): base(runner)
        {
        }

        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            Log.Info("Deploying certificate to WildFly");
            
            var envVariables = new Dictionary<string, string>(){
                {"OctopusEnvironment_Java_Certificate_Variable", variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable)},                
                {"OctopusEnvironment_Java_Certificate_Password", variables.Get(SpecialVariables.Action.Java.JavaKeystore.Password)},                               
                {"OctopusEnvironment_Java_Certificate_KeystoreFilename", variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreFilename)},
                {"OctopusEnvironment_Java_Certificate_KeystoreAlias", variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreAlias)},
                {"OctopusEnvironment_WildFly_Deploy_ServerType", variables.Get(SpecialVariables.Action.Java.WildFly.ServerType)},
                {"OctopusEnvironment_WildFly_Deploy_Controller", variables.Get(SpecialVariables.Action.Java.WildFly.Controller)},
                {"OctopusEnvironment_WildFly_Deploy_Port", variables.Get(SpecialVariables.Action.Java.WildFly.Port)},
                {"OctopusEnvironment_WildFly_Deploy_Protocol", variables.Get(SpecialVariables.Action.Java.WildFly.Protocol)},
                {"OctopusEnvironment_WildFly_Deploy_User", variables.Get(SpecialVariables.Action.Java.WildFly.User)},
                {"OctopusEnvironment_WildFly_Deploy_Password", variables.Get(SpecialVariables.Action.Java.WildFly.Password)},
                {"OctopusEnvironment_WildFly_Deploy_CertificateProfiles", variables.Get(SpecialVariables.Action.Java.WildFly.CertificateProfiles)},
                {"OctopusEnvironment_WildFly_Deploy_DeployCertificate", variables.Get(SpecialVariables.Action.Java.WildFly.DeployCertificate)},
                {"OctopusEnvironment_WildFly_Deploy_CertificateRelativeTo", variables.Get(SpecialVariables.Action.Java.WildFly.CertificateRelativeTo)},
                {"OctopusEnvironment_WildFly_Deploy_HTTPSPortBindingName", variables.Get(SpecialVariables.Action.Java.WildFly.HTTPSPortBindingName)},
                {"OctopusEnvironment_WildFly_Deploy_SecurityRealmName", variables.Get(SpecialVariables.Action.Java.WildFly.SecurityRealmName)},
                {"OctopusEnvironment_WildFly_Deploy_ElytronKeystoreName", variables.Get(SpecialVariables.Action.Java.WildFly.ElytronKeystoreName)},
                {"OctopusEnvironment_WildFly_Deploy_ElytronKeymanagerName", variables.Get(SpecialVariables.Action.Java.WildFly.ElytronKeymanagerName)},
                {"OctopusEnvironment_WildFly_Deploy_ElytronSSLContextName", variables.Get(SpecialVariables.Action.Java.WildFly.ElytronSSLContextName)}
            };

            if (variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) != null)
            {
                envVariables.Add("OctopusEnvironment_Java_Certificate_Private_Key",
                    variables.Get(variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) +
                                  ".PrivateKeyPem"));
                envVariables.Add("OctopusEnvironment_Java_Certificate_Public_Key",
                    variables.Get(variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) +
                                  ".CertificatePem"));
                envVariables.Add("OctopusEnvironment_Java_Certificate_Public_Key_Subject",
                    variables.Get(variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable) + ".Subject"));
            }

            runner.Run("com.octopus.calamari.wildflyhttps.WildflyHttpsStandaloneConfig", envVariables);

        }
    }
}