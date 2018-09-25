using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Integration.Nginx;
using Calamari.Integration.Processes;
using Newtonsoft.Json;
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
            var variables = deployment.Variables;

            var nginxServer = NginxServer.AutoDetect();

            var (rootLocation, additionalLocations) = GetLocations(variables);
            if (rootLocation == null) throw new NginxMissingRootLocationException();

            var enabledBindings = GetEnabledBindings(variables).ToList();
            var sslCertificates = GetSslCertificates(enabledBindings, variables);
            
            nginxServer
                .WithVirtualServerName(variables.Get(SpecialVariables.Package.NuGetPackageId))
                .WithHostName(variables.Get(SpecialVariables.Action.Nginx.Server.HostName))
                .WithServerBindings(enabledBindings, sslCertificates)
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(additionalLocations);

            Log.Verbose("Building nginx configuration");
            nginxServer.BuildConfiguration();

            Log.Verbose("Saving nginx configuration");
            nginxServer.SaveConfiguration();
        }

        IDictionary<string, (string SubjectCommonName, string CertificatePem, string PrivateKeyPem)> GetSslCertificates(IEnumerable<Binding> enabledBindings, CalamariVariableDictionary variables)
        {
            var sslCertsForEnabledBindings = new Dictionary<string, (string, string, string)>();
            foreach (var httpsBinding in enabledBindings.Where(b =>
                string.Equals("https", b.Protocol, StringComparison.InvariantCultureIgnoreCase) &&
                !string.IsNullOrEmpty(b.CertificateVariable)
            ))
            {
                var certificateVariable = httpsBinding.CertificateVariable;
                var subjectCommonName = variables.Get($"{certificateVariable}.SubjectCommonName");
                var certificatePem = variables.Get($"{certificateVariable}.CertificatePem");
                var privateKeyPem = variables.Get($"{certificateVariable}.PrivateKeyPem");
                sslCertsForEnabledBindings.Add(certificateVariable, (subjectCommonName, certificatePem, privateKeyPem));
            }

            return sslCertsForEnabledBindings;
        }

        static IEnumerable<Binding> GetEnabledBindings(VariableDictionary variables)
        {
            var bindingsString = variables.Get(SpecialVariables.Action.Nginx.Server.Bindings);
            if (string.IsNullOrWhiteSpace(bindingsString)) return new List<Binding>();

            return TryParseJson<Binding>(bindingsString, out var bindings)
                ? bindings.Where(b => b.Enabled)
                : new List<Binding>();
        }

        static (Location rootLocation, IEnumerable<Location> additionalLocations) GetLocations(VariableDictionary variables)
        {
            var locationsString = variables.Get(SpecialVariables.Action.Nginx.Server.Locations);
            if(string.IsNullOrWhiteSpace(locationsString)) return (null, new List<Location>());

            return TryParseJson<Location>(locationsString, out var locations)
                ? (locations.FirstOrDefault(l => string.Equals("/", l.Path)), locations.Where(l => !string.Equals("/", l.Path)))
                : (null, new List<Location>());
        }

        static bool TryParseJson<T>(string json, out IEnumerable<T> items)
        {
            try
            {
                items = JsonConvert.DeserializeObject<IEnumerable<T>>(json);
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
