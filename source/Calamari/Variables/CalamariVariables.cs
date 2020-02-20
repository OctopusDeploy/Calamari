using System;
using System.Collections.Generic;
using Calamari.Util;
using Octostache;

namespace Calamari.Variables
{
    public class CalamariVariables : VariableDictionary, IVariables
    {
        protected HashSet<string> SensitiveVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void SetSensitive(string name, string value)
        {
            if (name == null) return;
            Set(name, value);
            SensitiveVariableNames.Add(name);
        }

        public bool IsSensitive(string name)
        {
            return name != null && SensitiveVariableNames.Contains(name);
        }


        public string GetEnvironmentExpandedPath(string variableName, string defaultValue = null)
        {
            return CrossPlatform.ExpandPathEnvironmentVariables(Get(variableName, defaultValue));
        }

        public bool IsSet(string name)
        {
            return this[name] != null;
        }

        public void Merge(VariableDictionary other)
            => other.GetNames().ForEach(name => Set(name, other.GetRaw(name)));

        public IVariables Clone()
        {
            var dict = new CalamariVariables();
            dict.Merge(this);
            return dict;
        } 
    }
}