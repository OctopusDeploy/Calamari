using System;
using System.Collections.Generic;
using Calamari.Util;
using Octostache;

namespace Calamari.Variables
{
    public class CalamariVariables : VariableDictionary, IVariables
    {
        public bool IsSet(string name)
        {
            return this[name] != null;
        }

        public void Merge(VariableDictionary other)
            => other.GetNames().ForEach(name => Set(name, other.GetRaw(name)));

        public void AddFlag(string key, bool value)
            => Add(key, value.ToString());

        public IVariables Clone()
        {
            var dict = new CalamariVariables();
            dict.Merge(this);
            return dict;
        } 
    }
}