using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Integration.Certificates;
using Calamari.Integration.Iis;
using Octostache;

namespace Calamari.Deployment.Features
{
    public class IisWebSiteBeforeDeployFeature : IisWebSiteFeature
    {
        public override string DeploymentStage => DeploymentStages.BeforeDeploy;

        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            if (variables.GetFlag(SpecialVariables.Action.IisWebSite.DeployAsWebSite, false))
            {
                // Any certificate-variables used by IIS bindings must be placed in the 
                // LocalMachine certificate store
                EnsureCertificatesUsedInBindingsAreInStore(variables);
            }
        }

        static void EnsureCertificatesUsedInBindingsAreInStore(VariableDictionary variables)
        {
            foreach (var binding in GetBindings(variables))
            {
                string certificateVariable = binding.certificateVariable;

                if (string.IsNullOrWhiteSpace(certificateVariable))
                    continue;

                EnsureCertificateInStore(variables, certificateVariable.Trim());
            }
        }

        static void EnsureCertificateInStore(VariableDictionary variables, string certificateVariable)
        {
            var thumbprint = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Thumbprint}");

            if (CertificateExistsInLocalMachineStore(thumbprint))
            {
                Log.Verbose($"Certificate with thumbprint '{thumbprint}' already exists in Cert:\\LocalMachine");
                return;
            }

            AddCertificateToLocalMachineStore(variables, certificateVariable);
        }

        static bool CertificateExistsInLocalMachineStore(string thumbprint)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            return store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false).Count > 0;
        }

        static void AddCertificateToLocalMachineStore(VariableDictionary variables, string certificateVariable)
        {
            var pfxBytes = Convert.FromBase64String(variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Pfx}"));
            var password = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Password}");
            var subject = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Subject}"); 

            Log.Info($"Adding certificate '{subject}' into Cert:\\LocalMachine\\My");

            try
            {
                WindowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, StoreLocation.LocalMachine, "My", true);
            }
            catch (Exception)
            {
                Log.Error("Exception while attempting to add certificate to store");
                throw;
            }
        }
    }
}