using System;
using System.Collections.Generic;

namespace Octopus.Sashimi.Contracts.Variables
{
    public interface IImmutableVariableDictionary
    {
        int? GetInt32(string variableName);
        bool GetFlag(string variableName, bool defaultValueIfUnset = false);
        string Get(string variableName, string defaultValueIfUnset = null); // TODO: should this be GetIgnoringParseErrors?
        string GetRaw(string variableName);
        (string value, string errors) TryGet(string variableName);
        T GetEnum<T>(string variableName, T @default);
        string SaveAsString();
        string EvaluateIgnoringErrors(string expression);
        IList<string> GetStrings(string variableName, params char[] separators);
    }
}