using System.Collections.Generic;
using Calamari.Commands.Java;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Certificates.Java;

namespace Calamari.Deployment.Features.Java.Actions
{
    public class JavaKeystoreAction: JavaAction
    {
        readonly ILog log;

        public JavaKeystoreAction(JavaRunner runner, ILog log): base(runner)
        {
            this.log = log;
        }
        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            log.Info("Adding certificate to Java Keystore");

            var certificateId = variables.Get(SpecialVariables.Action.Java.JavaKeystore.Variable);
            var password = variables.Get(SpecialVariables.Action.Java.JavaKeystore.Password);
            var keystoreFilename = variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreFilename);
            var keystoreAlias = variables.Get(SpecialVariables.Action.Java.JavaKeystore.KeystoreAlias);
            var privateKeyPem = variables.Get(SpecialVariables.Certificate.PrivateKeyPem(certificateId));
            var certificatePem = variables.Get(SpecialVariables.Certificate.CertificatePem(certificateId));

            if (OctopusFeatureToggles.JavaKeystoreNativeBouncyCastleFeatureToggle.IsEnabled(variables))
            {
                var keystorePath = new JavaKeystoreBuilder(log).SaveKeystoreToFile(keystoreAlias, privateKeyPem, certificatePem, password, keystoreFilename);
                log.Info($"Keystore was successfully deployed to \"{keystorePath}\".");
                return;
            }

            var envVariables = new Dictionary<string, string>(){
                {"OctopusEnvironment_Java_Certificate_Variable", certificateId},
                {"OctopusEnvironment_Java_Certificate_Password", password},
                {"OctopusEnvironment_Java_Certificate_KeystoreFilename", keystoreFilename},
                {"OctopusEnvironment_Java_Certificate_KeystoreAlias", keystoreAlias},
                {"OctopusEnvironment_Java_Certificate_Private_Key", privateKeyPem},
                {"OctopusEnvironment_Java_Certificate_Public_Key", certificatePem},
                {"OctopusEnvironment_Java_Certificate_Public_Key_Subject", variables.Get(SpecialVariables.Certificate.Subject(certificateId))},
            };
            runner.Run("com.octopus.calamari.keystore.KeystoreConfig", envVariables);
        }
    }
}
