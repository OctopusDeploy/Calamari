using System.Collections.Generic;

namespace Calamari
{
    public interface IVariables : IEnumerable<KeyValuePair<string, string>>
    {
        bool IsSet(string name);
        void Set(string name, string value);
        void SetStrings(string variableName, IEnumerable<string> values, string separator);
        string GetRaw(string variableName);
        string Get(string variableName, string defaultValue = null);
        string Get(string variableName, out string error, string defaultValue = null);
        string Evaluate(string expressionOrVariableOrText, out string error, bool haltOnError = true);
        string Evaluate(string expressionOrVariableOrText);
        List<string> GetStrings(string variableName, params char[] separators);
        List<string> GetPaths(string variableName);
        bool GetFlag(string variableName, bool defaultValueIfUnset = false);
        int? GetInt32(string variableName);
        List<string> GetNames();
        List<string> GetIndexes(string variableCollectionName);
        void Add(string key, string value);
        void AddFlag(string key, bool value);
        string this[string name] { get; set; }
        IVariables Clone();
    }
}