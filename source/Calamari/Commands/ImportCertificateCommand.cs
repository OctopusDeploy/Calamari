using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;

namespace Calamari.Commands
{
    [Command("import-certificate", Description = "Imports a X.509 certificate into a Windows certificate store")]
    public class ImportCertificateCommand : Command
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public ImportCertificateCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            ImportCertificate(variables);

            return 0;
        }

        void ImportCertificate(CalamariVariableDictionary variables)
        {
            var certificateVariable = variables[SpecialVariables.Action.Certificate.CertificateVariable];

            if (string.IsNullOrWhiteSpace(certificateVariable))
            {
               throw new CommandException("Certificate variable was not supplied"); 
            }

            var pfxBytes =
                Convert.FromBase64String(
                    variables[
                        SpecialVariables.Action.Certificate.GetCertificateVariablePropertyName(certificateVariable, SpecialVariables.Action.Certificate.PfxProperty)]);
            var password =
                variables.Get(SpecialVariables.Action.Certificate.GetCertificateVariablePropertyName(certificateVariable, SpecialVariables.Action.Certificate.PasswordProperty));

            StoreLocation storeLocation;
            if (!Enum.TryParse(variables.Get(SpecialVariables.Action.Certificate.StoreLocation), out storeLocation))
            {
                throw new CommandException("Store location was not supplied");
            }

            var storeName = variables.Get(SpecialVariables.Action.Certificate.StoreName);
            if (string.IsNullOrWhiteSpace(storeName))
            {
                throw new CommandException("Store name was not supplied");
            }

            Log.Info($"Importing certificate from variable '{certificateVariable}' into store {storeLocation}/{storeName}");

            var privateKeyExportable = variables.GetFlag(SpecialVariables.Action.Certificate.PrivateKeyExportable, false);

            try
            {
                WindowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, storeLocation, storeName,
                    privateKeyExportable, GetPrivateKeyAccessRules(variables));
            }
            catch (Exception)
            {
                Log.Error("There was an error importing the certificate into the store");
                throw;
            }
        }

        static ICollection<PrivateKeyAccessRule> GetPrivateKeyAccessRules(CalamariVariableDictionary variables)
        {
            var json = variables.Get(SpecialVariables.Action.Certificate.PrivateKeyAccessRules);

            return string.IsNullOrWhiteSpace(json) ? new List<PrivateKeyAccessRule>() : PrivateKeyAccessRule.FromJson(json);
        }
    }
}