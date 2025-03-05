using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Security;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Identity.Client;

namespace Calamari.AzureServiceFabric.Integration
{
    public class AzureServiceFabricPowerShellContext : IScriptWrapper
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IVariables variables;
        readonly ILog log;

        readonly ScriptSyntax[] supportedScriptSyntax = {ScriptSyntax.PowerShell};

        public AzureServiceFabricPowerShellContext(IVariables variables, ILog log)
        {
            fileSystem = new WindowsPhysicalFileSystem();
            embeddedResources = new AssemblyEmbeddedResources();
            this.variables = variables;
            this.log = log;
        }

        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        public bool IsEnabled(ScriptSyntax syntax) =>
            !string.IsNullOrEmpty(variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint)) &&
            supportedScriptSyntax.Contains(syntax);

        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            // We only execute this hook if the connection endpoint has been set
            if (!IsEnabled(scriptSyntax))
            {
                throw new InvalidOperationException(
                    "This script wrapper hook is not enabled, and should not have been run");
            }

            if (!ServiceFabricHelper.IsServiceFabricSdkInstalled())
                throw new Exception("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusFabricTargetScript", script.File);
            variables.Set("OctopusFabricTargetScriptParameters", script.Parameters);

            var serverCertThumbprint = variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint);
            var connectionEndpoint = variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint);
            var aadUserCredUsername = variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername);
            var aadUserCredPassword = variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword);

            // Read thumbprint from our client cert variable (if applicable).
            var securityMode = variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode);
            var clientCertThumbprint = string.Empty;
            if (securityMode == AzureServiceFabricSecurityMode.SecureClientCertificate.ToString())
            {
                var certificateVariable = variables.GetMandatoryVariable(SpecialVariables.Action.ServiceFabric.ClientCertVariable);
                clientCertThumbprint = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Thumbprint}");
            }
            else if (securityMode == AzureServiceFabricSecurityMode.SecureAzureAD.ToString())
            {
                if (string.IsNullOrWhiteSpace(aadUserCredUsername))  return new CommandResult("Failed to find a value for the AAD username.", 0);
                if (string.IsNullOrWhiteSpace(aadUserCredPassword))  return new CommandResult("Failed to find a value for the AAD password.", 0);

                var aadTokenTask = GetAzureADAccessToken(serverCertThumbprint, connectionEndpoint, aadUserCredUsername, aadUserCredPassword);
                var aadToken = Task.Run(() => aadTokenTask).GetAwaiter().GetResult();

                SetOutputVariable("OctopusFabricAadToken", aadToken);
            }

            // Set output variables for our script to access.
            SetOutputVariable("OctopusFabricConnectionEndpoint", connectionEndpoint);
            SetOutputVariable("OctopusFabricSecurityMode", securityMode);
            SetOutputVariable("OctopusFabricServerCertThumbprint", serverCertThumbprint);
            SetOutputVariable("OctopusFabricClientCertThumbprint", clientCertThumbprint);
            SetOutputVariable("OctopusFabricCertificateFindType", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateFindType, "FindByThumbprint"));
            SetOutputVariable("OctopusFabricCertificateFindValueOverride", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateFindValueOverride));
            SetOutputVariable("OctopusFabricCertificateStoreLocation", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, "LocalMachine"));
            SetOutputVariable("OctopusFabricCertificateStoreName", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName, "MY"));

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, environmentVars);
            }
        }

        async Task<string> GetAzureADAccessToken(string serverCertThumbprint, string connectionEndpoint, string aadUserCredentialUsername, string aadUserCredentialPassword)
        {
            // Note that the following approach to retrieving the token is a little clunky - we're doing a 'GetClusterManifestAsync()' call, and nabbing the access token
            //  via the claim retrieval event handler.
            // Unfortunately, the FabricClient class is designed to hide the token (or at least the AzureActiveDirectoryMetadata used to get the token), outside the aforementioned event handler.
            // From what I can tell, there _is_ a REST call available to do this, and may well be what's used under the covers here, but I didn't want to add the complexity which that may involve.
            // This code is more-or-less the same as what is used for SF health checks, so we know that this approach does work.
            // Do note that if an AAD user is configured with MFA, these calls will fail with a message indicating such - we are unable to support MFA due to the requirement of user interaction
            //  and a redirect URI. That isn't new though.

            // This also doesn't implement support for AAD SecureClientCertificate, but AFAIK Service Fabric doesn't support that anyway, and we filter that out at our endpoint: https://github.com/OctopusDeploy/OctopusDeploy/blob/3ec629d30a8d820449460c4a05bd96a2fe63502f/source/Octopus.Server.Extensibility.Sashimi.AzureServiceFabric/Endpoints/AzureServiceFabricClusterEndpointValidator.cs#L28

            log.Info("Connecting with Secure Azure Active Directory");

            try
            {
                var claimsCredentials = new ClaimsCredentials();
                claimsCredentials.ServerThumbprints.Add(serverCertThumbprint);
                using var fabricClient = new FabricClient(claimsCredentials, connectionEndpoint); 
                
                string accessToken = null;
                fabricClient.ClaimsRetrieval += (o, e) =>
                {
                    accessToken = AzureADUsernamePasswordTokenRetriever.GetAccessToken(e.AzureActiveDirectoryMetadata, aadUserCredentialUsername, aadUserCredentialPassword, log);
                    return accessToken;
                };

                await fabricClient.ClusterManager.GetClusterManifestAsync();
                log.Verbose("Successfully received a response from the Service Fabric client");
                return accessToken;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get cluster manifest: {ex.PrettyPrint()}");
                throw;
            } 
        }
      

        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureServiceFabricContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.AzureServiceFabricContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        void SetOutputVariable(string name, string value)
        {
            if (variables.Get(name) != value)
            {
                log.SetOutputVariable(name, value, variables);
            }
        }
    }
}