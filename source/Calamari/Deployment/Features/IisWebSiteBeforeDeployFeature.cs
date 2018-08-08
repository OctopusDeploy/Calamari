using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.Certificates;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Octostache;

namespace Calamari.Deployment.Features
{
    public class IisWebSiteBeforeDeployFeature : IisWebSiteFeature
    {
        public override string DeploymentStage => DeploymentStages.BeforeDeploy;

        public override void Execute(IExecutionContext deployment)
        {
            var variables = deployment.Variables;

            if (variables.GetFlag(SpecialVariables.Action.IisWebSite.DeployAsWebSite, false))
            {

#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
                // Any certificate-variables used by IIS bindings must be placed in the 
                // LocalMachine certificate store
                EnsureCertificatesUsedInBindingsAreInStore(variables);
#endif

            }
        }


#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
        static void EnsureCertificatesUsedInBindingsAreInStore(VariableDictionary variables)
        {
            foreach (var binding in GetEnabledBindings(variables))
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

            var storeName = FindCertificateInLocalMachineStore(thumbprint);
            if (storeName != null)
            {
                Log.Verbose($"Found existing certificate with thumbprint '{thumbprint}' in Cert:\\LocalMachine\\{storeName}");
            }
            else
            {
                storeName = AddCertificateToLocalMachineStore(variables, certificateVariable);
            }

            Log.SetOutputVariable(SpecialVariables.Action.IisWebSite.Output.CertificateStoreName, storeName, variables);
        }

        static string FindCertificateInLocalMachineStore(string thumbprint)
        {
            foreach (var storeName in WindowsX509CertificateStore.GetStoreNames(StoreLocation.LocalMachine))
            {
                var store = new X509Store(storeName, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);

                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (found.Count != 0 && found[0].HasPrivateKey)
                {
                    return storeName;
                }

                store.Close();
            }

            return null;
        }

        static string AddCertificateToLocalMachineStore(VariableDictionary variables, string certificateVariable)
        {
            var pfxBytes = Convert.FromBase64String(variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Pfx}"));
            var password = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Password}");
            var subject = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Subject}"); 

            Log.Info($"Adding certificate '{subject}' into Cert:\\LocalMachine\\My");

            try
            {
                WindowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, StoreLocation.LocalMachine, "My", true);
                return "My";
            }
            catch (Exception)
            {
                Log.Error("Exception while attempting to add certificate to store");
                throw;
            }
        }
#endif

    }
}