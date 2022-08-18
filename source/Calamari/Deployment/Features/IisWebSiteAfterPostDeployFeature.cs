using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Certificates;
using Calamari.Integration.Iis;

namespace Calamari.Deployment.Features
{
    public class IisWebSiteAfterPostDeployFeature : IisWebSiteFeature
    {
        public override string DeploymentStage => DeploymentStages.AfterPostDeploy;

        public override void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            if (variables.GetFlag(SpecialVariables.Action.IisWebSite.DeployAsWebSite, false))
            {
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
                // For any bindings using certificate variables, the application pool account
                // must have access to the private-key.
                EnsureApplicationPoolHasCertificatePrivateKeyAccess(variables);
#endif
            }
        }

#if WINDOWS_CERTIFICATE_STORE_SUPPORT
        static void EnsureApplicationPoolHasCertificatePrivateKeyAccess(IVariables variables)
        {
            foreach (var binding in GetEnabledBindings(variables))
            {
                string certificateVariable = binding.certificateVariable;

                if (string.IsNullOrWhiteSpace(certificateVariable))
                    continue;

                var thumbprint = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Thumbprint}");
                var privateKeyAccess = CreatePrivateKeyAccessForApplicationPoolAccount(variables);

                // The store-name variable was set by IisWebSiteBeforePostDeploy
                var storeName = variables.Get(SpecialVariables.Action.IisWebSite.Output.CertificateStoreName);
                WindowsX509CertificateStore.AddPrivateKeyAccessRules(thumbprint, StoreLocation.LocalMachine, storeName,
                    new List<PrivateKeyAccessRule> {privateKeyAccess});
            }
        }

        static PrivateKeyAccessRule CreatePrivateKeyAccessForApplicationPoolAccount(IVariables variables)
        {
            var applicationPoolIdentityTypeValue = variables.Get(SpecialVariables.Action.IisWebSite.ApplicationPoolIdentityType);

            ApplicationPoolIdentityType appPoolIdentityType;
            if(!Enum.TryParse(applicationPoolIdentityTypeValue, out appPoolIdentityType))
            {
                throw new CommandException($"Unexpected value for '{SpecialVariables.Action.IisWebSite.ApplicationPoolIdentityType}': '{applicationPoolIdentityTypeValue}'");
            }

            return new PrivateKeyAccessRule(
                GetIdentityForApplicationPoolIdentity(appPoolIdentityType, variables),
                PrivateKeyAccess.FullControl);
        }

        static IdentityReference GetIdentityForApplicationPoolIdentity(ApplicationPoolIdentityType applicationPoolIdentityType,
            IVariables variables)
        {
            switch (applicationPoolIdentityType)
            {
                case ApplicationPoolIdentityType.ApplicationPoolIdentity:
                    return new NTAccount("IIS AppPool\\" + variables.Get(SpecialVariables.Action.IisWebSite.ApplicationPoolName));

                case ApplicationPoolIdentityType.LocalService:
                    return new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);

                case ApplicationPoolIdentityType.LocalSystem:
                    return new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);

                case ApplicationPoolIdentityType.NetworkService:
                    return new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

                case ApplicationPoolIdentityType.SpecificUser:
                    return new NTAccount(StripLocalAccountIdentifierFromUsername(variables.Get(SpecialVariables.Action.IisWebSite.ApplicationPoolUserName)));

                default:
                    throw new ArgumentOutOfRangeException(nameof(applicationPoolIdentityType), applicationPoolIdentityType, null);
            }
        }

        static string StripLocalAccountIdentifierFromUsername(string username)
        {
            //The NTAccount class doesnt work with local accounts represented in the format of .\username
            //an exception is thrown when attempting to call NTAccount.Translate().
            //The following expression is to remove .\ from the beginning of usernames, we still allow for usernames in the format of machine\user or domain\user
            return Regex.Replace(username, "\\.\\\\(.*)", "$1", RegexOptions.None);
        }
#endif

    }
}