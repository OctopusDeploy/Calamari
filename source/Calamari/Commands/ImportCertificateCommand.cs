﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.Certificates;

namespace Calamari.Commands
{
    [Command("import-certificate", Description = "Imports a X.509 certificate into a Windows certificate store")]
    public class ImportCertificateCommand : Command
    {
        readonly IVariables variables;
        readonly IWindowsX509CertificateStore windowsX509CertificateStore;
        readonly ILog log;

        public ImportCertificateCommand(IVariables variables, IWindowsX509CertificateStore windowsX509CertificateStore, ILog log)
        {
            this.variables = variables;
            this.windowsX509CertificateStore = windowsX509CertificateStore;
            this.log = log;
        }

        public override int Execute(string[] commandLineArguments)
        {
            ImportCertificate();

            return 0;
        }

        void ImportCertificate()
        {
            var certificateVariable = variables.GetMandatoryVariable(SpecialVariables.Action.Certificate.CertificateVariable);
            var pfxBytes = Convert.FromBase64String(variables.GetMandatoryVariable($"{certificateVariable}.{CertificateVariables.Properties.Pfx}"));
            var password = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Password}");
            var thumbprint = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Thumbprint}");
            var storeName = variables.GetMandatoryVariable(SpecialVariables.Action.Certificate.StoreName);
            var privateKeyExportable = variables.GetFlag(SpecialVariables.Action.Certificate.PrivateKeyExportable, false);

            // Either a store-location (LocalMachine or CurrentUser) or a user can be supplied
            StoreLocation storeLocation;
            var locationSpecified = Enum.TryParse(variables.Get(SpecialVariables.Action.Certificate.StoreLocation), out storeLocation);

            ValidateStore(locationSpecified ? (StoreLocation?)storeLocation : null, storeName);

            try
            {
                if (locationSpecified)
                {
                    log.Info(
                        $"Importing certificate '{variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Subject}")}' with thumbprint '{thumbprint}' into store '{storeLocation}\\{storeName}'");
                    windowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, storeLocation, storeName, privateKeyExportable);

                    if (storeLocation == StoreLocation.LocalMachine)
                    {
                        // Set private-key access
                        var privateKeyAccessRules = GetPrivateKeyAccessRules(variables);
                        if (privateKeyAccessRules.Any())
                            windowsX509CertificateStore.AddPrivateKeyAccessRules(thumbprint, storeLocation, storeName, privateKeyAccessRules);
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

                    log.Info(
                        $"Importing certificate '{variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Subject}")}' with thumbprint '{thumbprint}' into store '{storeName}' for user '{storeUser}'");
                    windowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, storeUser, storeName, privateKeyExportable);
                }

            }
            catch (Exception)
            {
                log.Error("There was an error importing the certificate into the store");
                throw;
            }
        }

        internal static ICollection<PrivateKeyAccessRule> GetPrivateKeyAccessRules(IVariables variables)
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