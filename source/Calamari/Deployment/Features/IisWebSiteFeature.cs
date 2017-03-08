using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Deployment.Features
{
    public abstract class IisWebSiteFeature : IFeature
    {
        public string Name => "Octopus.Features.IISWebSite";
        public abstract string DeploymentStage { get; }
        public abstract void Execute(RunningDeployment deployment);

        protected static IEnumerable<dynamic> GetBindings(VariableDictionary variables)
        {
            var bindingString = variables.Get(SpecialVariables.Action.IisWebSite.Bindings);

            if (string.IsNullOrWhiteSpace(bindingString))
                return new List<dynamic>();

            dynamic bindings;

            return TryParseJson(bindingString, out bindings) 
                ? bindings:
                new List<dynamic>();
        }

        static bool TryParseJson(string json, out dynamic bindings)
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