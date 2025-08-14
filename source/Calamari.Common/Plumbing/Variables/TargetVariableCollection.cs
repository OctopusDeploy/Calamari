using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Variables
{

    public class CalamariExecutionVariableCollection : List<CalamariExecutionVariable>
    {
        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.None
        };

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None, SerializerSettings);
        }

        public static CalamariExecutionVariableCollection FromJson(string json)
        {
            return JsonConvert.DeserializeObject<CalamariExecutionVariableCollection>(json, SerializerSettings)
                   ?? throw new InvalidOperationException("Failed to deserialize target variables from json.");
        }
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class CalamariExecutionVariable
    {
        public CalamariExecutionVariable(string key, string? value, bool isSensitive)
        {
            Key = key;
            Value = value;
            IsSensitive = isSensitive;
        }

        [JsonProperty("key")]
        public string Key { get; private set; }

        [JsonProperty("value", DefaultValueHandling = DefaultValueHandling.Include)]
        public string? Value { get; set; }

        [JsonProperty("isSensitive")]
        public bool IsSensitive { get; set; }

        string DebuggerDisplay => $"{Key}={Value} {(IsSensitive ? "(Sensitive)" : null)}";
    }
 
}