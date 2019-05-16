﻿#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Octostache;


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
            var certificateVariable = GetMandatoryVariable(variables, SpecialVariables.Action.Certificate.CertificateVariable);
            var pfxBytes = Convert.FromBase64String(GetMandatoryVariable(variables, $"{certificateVariable}.{SpecialVariables.Certificate.Properties.Pfx}"));
            var password = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Password}");
            var thumbprint = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Thumbprint}");
            var storeName = GetMandatoryVariable(variables, SpecialVariables.Action.Certificate.StoreName);
            var privateKeyExportable = variables.GetFlag(SpecialVariables.Action.Certificate.PrivateKeyExportable, false);

            // Either a store-location (LocalMachine or CurrentUser) or a user can be supplied
            StoreLocation storeLocation;
            var locationSpecified = Enum.TryParse(variables.Get(SpecialVariables.Action.Certificate.StoreLocation), out storeLocation);

            ValidateStore(locationSpecified ? (StoreLocation?)storeLocation : null, storeName);

            try
            {
                if (locationSpecified)
                {
                    Log.Info(
                        $"Importing certificate '{variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Subject}")}' with thumbprint '{thumbprint}' into store '{storeLocation}\\{storeName}'");
                    WindowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, storeLocation, storeName,
                        privateKeyExportable);

                    if (storeLocation == StoreLocation.LocalMachine)
                    {
                        // Set private-key access
                        var privateKeyAccessRules = GetPrivateKeyAccessRules(variables);
                        if (privateKeyAccessRules.Any())
                            WindowsX509CertificateStore.AddPrivateKeyAccessRules(thumbprint, storeLocation, storeName,
                                privateKeyAccessRules);
                    }
                }
                else // Import into a specific user's store
                {
                    var storeUser = variables.Get(SpecialVariables.Action.Certificate.StoreUser);

                    if (string.IsNullOrWhiteSpace(storeUser))
                    {
                        throw new CommandException(
                            $"Either '{SpecialVariables.Action.Certificate.StoreLocation}' or '{SpecialVariables.Action.Certificate.StoreUser}' must be supplied");
                    }

                    Log.Info(
                        $"Importing certificate '{variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Subject}")}' with thumbprint '{thumbprint}' into store '{storeName}' for user '{storeUser}'");
                    WindowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, storeUser, storeName,
                        privateKeyExportable);
                }

            }
            catch (Exception)
            {
                Log.Error("There was an error importing the certificate into the store");
                throw;
            }
        }

        internal static ICollection<PrivateKeyAccessRule> GetPrivateKeyAccessRules(VariableDictionary variables)
        {
            // The private-key access-rules are stored as escaped JSON. However, they may contain nested
            // variables (for example the user-name may be an Octopus variable) which may not be escaped,
            // causing JSON parsing to fail.

            // So, we get the raw text
            var raw = variables.GetRaw(SpecialVariables.Certificate.PrivateKeyAccessRules);

            if (string.IsNullOrWhiteSpace(raw))
                return new List<PrivateKeyAccessRule>();

            // Unescape it (we only care about backslashes)
            var unescaped = raw.Replace(@"\\", @"\");
            // Perform variable-substitution and re-escape
            var escapedAndSubstituted = variables.Evaluate(unescaped).Replace(@"\", @"\\");
            return PrivateKeyAccessRule.FromJson(escapedAndSubstituted);
        }

        string GetMandatoryVariable(CalamariVariableDictionary variables, string variableName)
        {
            var value = variables.Get(variableName);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new CommandException($"Variable {variableName} was not supplied");
            }

            return value;
        }

        static void ValidateStore(StoreLocation? storeLocation, string storeName)
        {
            // Windows wants to launch an interactive confirmation dialog when importing into the Root store for a user.
            // https://github.com/OctopusDeploy/Issues/issues/3347
            if ((!storeLocation.HasValue || storeLocation.Value != StoreLocation.LocalMachine)
                && storeName == WindowsX509CertificateStore.RootAuthorityStoreName)
            {
                throw new CommandException($"When importing certificate into {WindowsX509CertificateStore.RootAuthorityStoreName} store, location must be '{StoreLocation.LocalMachine}'. " +
                    $"Windows security restrictions prevent importing into the {WindowsX509CertificateStore.RootAuthorityStoreName} store for a user.");
            }
        }
    }
}
#endif