using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Sashimi.Server.Contracts.Variables
{
    public interface IImmutableVariableDictionary
    {
        int? GetInt32(string variableName);
        bool GetFlag(string variableName, bool defaultValueIfUnset = false);

        [return: NotNullIfNotNull("defaultValueIfUnset")]
        string? Get(string variableName, string? defaultValueIfUnset = null); // TODO: should this be GetIgnoringParseErrors?

        string? GetRaw(string variableName);
        (string? value, string? errors) TryGet(string variableName);
        T GetEnum<T>(string variableName, T @default) where T : Enum;

        string SaveAsString();

        string? EvaluateIgnoringErrors(string? expression);

        IList<string> GetStrings(string variableName, params char[] separators);
    }
}