using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Variables
{
    public class TargetVariableCollection : List<TargetVariable>
    {
        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None
        };

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None, SerializerSettings);
        }

        public static TargetVariableCollection FromJson(string json)
        {
            return JsonConvert.DeserializeObject<TargetVariableCollection>(json, SerializerSettings)
                   ?? throw new InvalidOperationException("Failed to deserialize target variables from json.");
        }
    }

    public class TargetVariable
    {
        public TargetVariable(string key, string? value, bool isSensitive)
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
    }
}