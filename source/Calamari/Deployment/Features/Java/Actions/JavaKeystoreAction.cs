using System.Collections.Generic;
using Calamari.Commands.Java;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class JavaKeystoreAction: JavaAction
    {
        public JavaKeystoreAction(JavaRunner runner): base(runner)
        {
        }
        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            Log.Info("Adding certificate to Java Keystore");
            
            var certificateId = variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable);
            var envVariables = new Dictionary<string, string>(){
                {"OctopusEnvironment_Java_Certificate_Variable", certificateId},                
                {"OctopusEnvironment_Java_Certificate_Password", variables.Get(SpecialVariables.Action.Java.JavaKeystore.Password)},                               
                {"OctopusEnvironment_Java_Certificate_KeystoreFilename", variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreFilename)},
                {"OctopusEnvironment_Java_Certificate_KeystoreAlias", variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreAlias)},
                {"OctopusEnvironment_Java_Certificate_Private_Key", variables.Get(SpecialVariables.Certificate.PrivateKeyPem(certificateId))},
                {"OctopusEnvironment_Java_Certificate_Public_Key", variables.Get(SpecialVariables.Certificate.CertificatePem(certificateId))},
                {"OctopusEnvironment_Java_Certificate_Public_Key_Subject", variables.Get(SpecialVariables.Certificate.Subject(certificateId))},
            };
            runner.Run("com.octopus.calamari.keystore.KeystoreConfig", envVariables);
        }
    }
}