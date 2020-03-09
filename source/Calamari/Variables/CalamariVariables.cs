using System;
using System.Collections.Generic;
using Calamari.Util;
using Octostache;

namespace Calamari.Variables
{
    public class CalamariVariables : VariableDictionary, IVariables
    {
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