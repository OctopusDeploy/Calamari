using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Deployment.Features
{
    public abstract class IisWebSiteFeature : IFeature
    {
        public string Name => "Octopus.Features.IISWebSite";
        public abstract string DeploymentStage { get; }
        public abstract void Execute(RunningDeployment deployment);

        protected static IEnumerable<dynamic> GetEnabledBindings(IVariables variables)
        {
            var bindingString = variables.Get(SpecialVariables.Action.IisWebSite.Bindings);

            if (string.IsNullOrWhiteSpace(bindingString))
                return new List<dynamic>();

            IEnumerable<dynamic> bindings;

            return TryParseJson(bindingString, out bindings) 
                ? bindings.Where(binding => bool.Parse((string)binding.enabled))
                : new List<dynamic>();
        }

        static bool TryParseJson(string json, out IEnumerable<dynamic> bindings)
        {
            try
            {
                bindings = JArray.Parse(json);
                return true;
            }
            catch
            {
                bindings = null;
                return false;
            }
        }
    }
}