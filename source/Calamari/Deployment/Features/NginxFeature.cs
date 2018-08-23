using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Integration.Nginx;
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

            nginxServer
                .WithVirtualServerName(variables.Get(SpecialVariables.Package.NuGetPackageId))
                .WithHostName(variables.Get(SpecialVariables.Action.Nginx.Server.HostName))
                .WithServerBindings(GetEnabledBindings(variables))
                .WithRootLocation(rootLocation)
                .WithAdditionalLocations(additionalLocations);

            Log.Verbose("Building nginx configuration");
            nginxServer.BuildConfiguration();

            Log.Verbose("Saving nginx configuration");
            nginxServer.SaveConfiguration();

            // Test nginx config? i.e. nginx -t
            if (!nginxServer.ValidateConfiguration(out var validateConfigError))
            {
                throw new NginxConfigurationValidationFailedException(validateConfigError);
            }

            // Reload nginx? i.e. nginx -s reload
            if (!nginxServer.ReloadConfiguration(out var reloadConfigError))
            {
                throw new NginxReloadConfigurationFailedException(reloadConfigError);
            }
        }

        protected static IEnumerable<dynamic> GetEnabledBindings(VariableDictionary variables)
        {
            var bindingsString = variables.Get(SpecialVariables.Action.Nginx.Server.Bindings);
            if (string.IsNullOrWhiteSpace(bindingsString)) return new List<dynamic>();

            return TryParseJson(bindingsString, out var bindings)
                ? bindings.Where(b => bool.Parse((string)b.enabled))
                : new List<dynamic>();
        }

        protected static (dynamic rootLocation, IEnumerable<dynamic> additionalLocations) GetLocations(VariableDictionary variables)
        {
            var locationsString = variables.Get(SpecialVariables.Action.Nginx.Server.Locations);
            if(string.IsNullOrWhiteSpace(locationsString)) return (null, new List<dynamic>());

            return TryParseJson(locationsString, out var locations)
                ? (locations.FirstOrDefault(l => string.Equals("/", (string)l.path)), locations.Where(l => !string.Equals("/", (string)l.path)))
                : (null, new List<dynamic>());
        }

        private static bool TryParseJson(string json, out IEnumerable<dynamic> items)
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
