using System;
using System.Collections.Generic;
using Octostache;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Tests.Shared.Server
{
    public class TestVariableDictionary : VariableDictionary, IActionAndTargetScopedVariables
    {
        public (string? value, string? errors) TryGet(string variableName)
        {
            var value = Get(variableName, out var errors);
            return (value, errors);
        }

        public T GetEnum<T>(string value, T defaultValue) where T : Enum
        {
            return (T)Enum.Parse(typeof(T), Get(value, defaultValue.ToString()));
        }

        public string? EvaluateIgnoringErrors(string? expression)
        {
            return Evaluate(expression);
        }

        IList<string> IImmutableVariableDictionary.GetStrings(string variableName, params char[] separators)
        {
            return GetStrings(variableName, separators);
        }
    }
}