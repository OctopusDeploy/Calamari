using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Nginx;
using Newtonsoft.Json;

namespace Calamari.Deployment.Features
{
    public class NginxFeature : IFeature
    {
        static readonly string[] RootLocations = { "= /", "/" };
        public string Name => "Octopus.Features.Nginx";
        public string DeploymentStage => DeploymentStages.AfterDeploy;

        readonly NginxServer nginxServer;
        readonly ICalamariFileSystem fileSystem;

        public NginxFeature(NginxServer nginxServer, ICalamariFileSystem fileSystem)
        {
            this.nginxServer = nginxServer;
            this.fileSystem = fileSystem;
        }

        public void Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            var (rootLocation, additionalLocations) = GetLocations(variables);
            if (rootLocation == null) throw new NginxMissingRootLocationException();

            var enabledBindings = GetEnabledBindings(variables).ToList();
            var sslCertificates = GetSslCertificates(enabledBindings, variables);
            var customNginxSslRoot = variables.Get(SpecialVariables.Action.Nginx.SslRoot);
            /*
             * Previous versions of the NGINX step did not expose the ability to define the file names of the configuration
             * files, and instead used the Package ID. This meant that multi-tenanted deployments that shared the same
             * package would overwrite each other when deployed to the same machine.
             *
             * To retain compatibility with existing deployments, the Package ID is still used as a default file name.
             * But if the config name setting has been defined, that is used instead.
             *
             * See https://github.com/OctopusDeploy/Issues/issues/6216
             */
            var virtualServerName =
                string.IsNullOrWhiteSpace(variables.Get(SpecialVariables.Action.Nginx.Server.ConfigName))
                    ? variables.Get(PackageVariables.PackageId)
                    : variables.Get(SpecialVariables.Action.Nginx.Server.ConfigName);

            nginxServer
                .WithVirtualServerName(virtualServerName)
                .WithHostName(variables.Get(SpecialVariables.Action.Nginx.Server.HostName))
                .WithServerBindings(enabledBindings, sslCertificates, customNginxSslRoot)
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(additionalLocations);

            Log.Verbose("Building nginx configuration");
            var customNginxConfRoot = variables.Get(SpecialVariables.Action.Nginx.ConfigRoot);
            nginxServer.BuildConfiguration(customNginxConfRoot);

            Log.Verbose("Saving nginx configuration");
            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            variables.Set("OctopusNginxFeatureTempDirectory", tempDirectory);
            nginxServer.SaveConfiguration(tempDirectory);
        }

        IDictionary<string, (string SubjectCommonName, string CertificatePem, string PrivateKeyPem)> GetSslCertificates(IEnumerable<Binding> enabledBindings, IVariables variables)
        {
            var sslCertsForEnabledBindings = new Dictionary<string, (string, string, string)>();
            foreach (var httpsBinding in enabledBindings.Where(b =>
                string.Equals("https", b.Protocol, StringComparison.OrdinalIgnoreCase) &&
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

        static IEnumerable<Binding> GetEnabledBindings(IVariables variables)
        {
            var bindingsString = variables.Get(SpecialVariables.Action.Nginx.Server.Bindings);
            if (string.IsNullOrWhiteSpace(bindingsString)) return new List<Binding>();

            return TryParseJson<Binding>(bindingsString, out var bindings)
                ? bindings.Where(b => b.Enabled)
                : new List<Binding>();
        }

        static (Location rootLocation, IEnumerable<Location> additionalLocations) GetLocations(IVariables variables)
        {
            var locationsString = variables.Get(SpecialVariables.Action.Nginx.Server.Locations);
            if(string.IsNullOrWhiteSpace(locationsString)) return (null, new List<Location>());

            if (!TryParseJson<Location>(locationsString, out var locations))
                throw new NginxMissingRootLocationException();

            var rootLocation = locations.FirstOrDefault(l => RootLocations.Contains(l.Path));
            if (rootLocation == null) throw new NginxMissingRootLocationException();
            return (rootLocation, locations.Where(l => !string.Equals(rootLocation.Path, l.Path)));
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
