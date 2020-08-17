using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.FunctionScriptContributions
{
    public class ScriptFunctionRegistration
    {
        public string Name { get; }
        public string Description { get; }
        public string ServiceMessageName { get; }
        public IDictionary<string, FunctionParameter> Parameters { get; }

        public ScriptFunctionRegistration(
            string name,
            string description,
            string serviceMessageName,
            IDictionary<string, FunctionParameter> parameters)
        {
            Name = name;
            Description = description;
            ServiceMessageName = serviceMessageName;
            Parameters = parameters;
        }
    }
}