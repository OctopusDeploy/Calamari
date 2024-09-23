#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Calamari.Tests.Helpers.Certificates;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Certificates
{
    public class ImportCertificateCommandFixture : CalamariFixture
    {
        readonly string certificateVariable = "FooCert";
        readonly string randomSubject = $"Subject-{Guid.NewGuid():d}";
        readonly SampleCertificate cert = SampleCertificate.CapiWithPrivateKey;

        [Test]
        public void AddingCertToRoot_ThrowsError()
        {
            var variables = CreateInitialVariables();
            variables.Add(SpecialVariables.Action.Certificate.StoreName, StoreName.Root.ToString());
            variables.Add(SpecialVariables.Action.Certificate.StoreLocation, StoreLocation.CurrentUser.ToString());

            var result = Invoke(variables);

            result.AssertFailure();
            result.AssertErrorOutput("When importing certificate into Root store, location must be 'LocalMachine'. Windows security restrictions prevent importing into the Root store for a user.");
        }
        
        [Test]
        public void AddingCertToStore_AddsCert()
        {
            var certificateStoreLocation = StoreLocation.LocalMachine;
            var storeName = StoreName.My.ToString();
            var variables = CreateInitialVariables();
            variables.Add(SpecialVariables.Action.Certificate.StoreName, storeName);
            variables.Add(SpecialVariables.Action.Certificate.StoreLocation, certificateStoreLocation.ToString());
            cert.EnsureCertificateNotInStore(storeName, certificateStoreLocation);

            var result = Invoke(variables);

            result.AssertSuccess();
            result.AssertOutputContains($"Importing certificate '{randomSubject}' with thumbprint '{cert.Thumbprint}' into store 'LocalMachine\\My'");
            result.AssertOutputContains("Imported certificate 'CN=www.acme.com' into store 'My'");
            cert.AssertCertificateIsInStore(storeName, certificateStoreLocation);
            
            // Hygiene Cleanup
            cert.EnsureCertificateNotInStore(storeName, certificateStoreLocation);
        }

        [Test]
        public void NoStoreLocationProvided_StoresInUserName()
        {
            var storeName = StoreName.My.ToString();
            var storeLocation = StoreLocation.CurrentUser;
            var userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            var variables = CreateInitialVariables();
            variables.Add(SpecialVariables.Action.Certificate.StoreName, storeName);
            variables.Add(SpecialVariables.Action.Certificate.StoreUser, userName);
            cert.EnsureCertificateNotInStore(storeName, storeLocation);
            
            var result = Invoke(variables);

            result.AssertSuccess();
            result.AssertOutputContains($"Importing certificate '{randomSubject}' with thumbprint '{cert.Thumbprint}' into store 'My' for user '{userName}'");
            result.AssertOutputMatches("Imported certificate 'CN=www.acme.com' into store 'S-.*\\\\My'");
            cert.AssertCertificateIsInStore(storeName, storeLocation);

            // Hygiene Cleanup
            cert.EnsureCertificateNotInStore(storeName, storeLocation);
        }
        
        CalamariResult Invoke(VariableDictionary variables)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);
                return Invoke(Calamari()
                              .Action("import-certificate")
                              .Argument("variables", variablesFile.FilePath));
            }
        }
        
        VariableDictionary CreateInitialVariables()
        {
            var variables = new VariableDictionary()
            {
                [SpecialVariables.Action.Certificate.CertificateVariable] = certificateVariable,
                [$"{certificateVariable}.{CertificateVariables.Properties.Pfx}"] = cert.Base64Bytes(),
                [$"{certificateVariable}.{CertificateVariables.Properties.Password}"] = cert.Password,
                [$"{certificateVariable}.{CertificateVariables.Properties.Thumbprint}"] = cert.Thumbprint,
                [$"{certificateVariable}.{CertificateVariables.Properties.Subject}"] = randomSubject,
            };
            return variables;
        }
    }
}
#endif