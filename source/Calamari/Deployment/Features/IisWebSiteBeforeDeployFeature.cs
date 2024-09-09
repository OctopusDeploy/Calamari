using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Certificates;

namespace Calamari.Deployment.Features
{
    public class IisWebSiteBeforeDeployFeature : IisWebSiteFeature
    {
        readonly IWindowsX509CertificateStore windowsX509CertificateStore;
        public override string DeploymentStage => DeploymentStages.BeforeDeploy;

        public IisWebSiteBeforeDeployFeature(IWindowsX509CertificateStore windowsX509CertificateStore)
        {
            this.windowsX509CertificateStore = windowsX509CertificateStore;
        }
        
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


        void EnsureCertificatesUsedInBindingsAreInStore(IVariables variables)
        {
            foreach (var binding in GetEnabledBindings(variables))
            {
                string certificateVariable = binding.certificateVariable;

                if (string.IsNullOrWhiteSpace(certificateVariable))
                    continue;

                EnsureCertificateInStore(variables, certificateVariable.Trim());
            }
        }

        void EnsureCertificateInStore(IVariables variables, string certificateVariable)
        {
            var thumbprint = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Thumbprint}");

            var storeName = windowsX509CertificateStore.FindCertificateStore(thumbprint, StoreLocation.LocalMachine);
            if (storeName != null)
            {
                Log.Verbose($"Found existing certificate with thumbprint '{thumbprint}' in Cert:\\LocalMachine\\{storeName}");
            }
            else
            {
                storeName = AddCertificateToLocalMachineStore(variables, certificateVariable);
            }

            var storeNamesVariable = variables.Get(SpecialVariables.Action.IisWebSite.Output.CertificateStoreName, "") ?? "";
            if (storeNamesVariable.Split(',').Contains(storeName))
                return;

            storeNamesVariable = string.Join(",", storeNamesVariable, storeName);
            Log.SetOutputVariable(SpecialVariables.Action.IisWebSite.Output.CertificateStoreName, storeNamesVariable, variables);
        }

        string AddCertificateToLocalMachineStore(IVariables variables, string certificateVariable)
        {
            var pfxBytes = Convert.FromBase64String(variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Pfx}"));
            var password = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Password}");
            var subject = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Subject}");

            Log.Info($"Adding certificate '{subject}' into Cert:\\LocalMachine\\My");

            try
            {
                windowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, StoreLocation.LocalMachine, "My", true);
                return "My";
            }
            catch (Exception)
            {
                Log.Error("Exception while attempting to add certificate to store");
                throw;
            }
        }


    }
}