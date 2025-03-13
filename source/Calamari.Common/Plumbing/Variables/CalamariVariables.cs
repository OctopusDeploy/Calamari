using System;
using Calamari.Common.Commands;
using Octostache;

namespace Calamari.Common.Plumbing.Variables
{
    public class CalamariVariables : VariableDictionary, IVariables
    {
        public bool IsSet(string name)
        {
            return this[name] != null;
        }

        public void Merge(VariableDictionary other)
        {
            other.GetNames().ForEach(name => Set(name, other.GetRaw(name)));
        }

        public void AddFlag(string key, bool value)
        {
            Add(key, value.ToString());
        }

        public IVariables Clone()
        {
            var dict = new CalamariVariables();
            dict.Merge(this);
            return dict;
        }

        public IVariables CloneAndEvaluate()
        {
            var dict = new CalamariVariables();
            GetNames().ForEach(name => dict.Set(name, Get(name)));
            return dict;
        }

        public string GetMandatoryVariable(string variableName)
        {
            var value = Get(variableName);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new CommandException($"Variable {variableName} was not supplied");
            }

            return value;
        }
    }
}