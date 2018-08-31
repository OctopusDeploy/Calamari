using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Integration.Nginx;
using Calamari.Integration.Processes;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Deployment.Features
{
    public class NginxFeature : IFeature
    {
        public string Name => "Octopus.Features.Nginx";
        public string DeploymentStage => DeploymentStages.AfterDeploy;

        public void Execute(RunningDeployment deployment)
        {
            Log.Info("Running nginx feature");
            var nginxServer = NginxServer.AutoDetect();

            var variables = deployment.Variables;

            var (rootLocation, additionalLocations) = GetLocations(variables);
            if (rootLocation == null) throw new NginxMissingRootLocationException();

            var enabledBindings = GetEnabledBindings(variables).ToList();
            var sslCertificates = GetSslCertificates(enabledBindings, variables);
            
            nginxServer.WithVirtualServerName(variables.Get(SpecialVariables.Package.NuGetPackageId))
                .WithHostName(variables.Get(SpecialVariables.Action.Nginx.Server.HostName))
                .WithServerBindings(enabledBindings, sslCertificates)
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(additionalLocations);

            Log.Verbose("Building nginx configuration");
            nginxServer.BuildConfiguration();

            Log.Verbose("Saving nginx configuration");
            nginxServer.SaveConfiguration();
        }

        IDictionary<string, (string SubjectCommonName, string CertificatePem, string PrivateKeyPem)> GetSslCertificates(IEnumerable<dynamic> enabledBindings, CalamariVariableDictionary variables)
        {
            var sslCertsForEnabledBindings = new Dictionary<string, (string, string, string)>();
            foreach (var httpsBinding in enabledBindings.Where(b =>
                string.Equals("https", (string) b.protocol, StringComparison.InvariantCultureIgnoreCase) &&
                !string.IsNullOrEmpty((string)b.certificateVariable)
            ))
            {
                var certificateVariable = (string) httpsBinding.certificateVariable;
                var subjectCommonName = variables.Get($"{certificateVariable}.SubjectCommonName");
                var certificatePem = variables.Get($"{certificateVariable}.CertificatePem");
                var privateKeyPem = variables.Get($"{certificateVariable}.PrivateKeyPem");
                sslCertsForEnabledBindings.Add(certificateVariable, (subjectCommonName, certificatePem, privateKeyPem));
            }

            return sslCertsForEnabledBindings;
        }

        static IEnumerable<dynamic> GetEnabledBindings(VariableDictionary variables)
        {
            var bindingsString = variables.Get(SpecialVariables.Action.Nginx.Server.Bindings);
            if (string.IsNullOrWhiteSpace(bindingsString)) return new List<dynamic>();

            return TryParseJson(bindingsString, out var bindings)
                ? bindings.Where(b => bool.Parse((string)b.enabled))
                : new List<dynamic>();
        }

        static (dynamic rootLocation, IEnumerable<dynamic> additionalLocations) GetLocations(VariableDictionary variables)
        {
            var locationsString = variables.Get(SpecialVariables.Action.Nginx.Server.Locations);
            if(string.IsNullOrWhiteSpace(locationsString)) return (null, new List<dynamic>());

            return TryParseJson(locationsString, out var locations)
                ? (locations.FirstOrDefault(l => string.Equals("/", (string)l.path)), locations.Where(l => !string.Equals("/", (string)l.path)))
                : (null, new List<dynamic>());
        }

        static bool TryParseJson(string json, out IEnumerable<dynamic> items)
        {
            try
            {
                items = JArray.Parse(json);
                return true;
            }
            catch
            {
                items = null;
                return false;
            }
        }
    }
}
