using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.Integration.Certificates
{
    public class PrivateKeyAccessRule
    {
        public PrivateKeyAccessRule(string identity, PrivateKeyAccess access)
        {
            Identity = identity;
            Access = access;
        }

        public string Identity { get; }
        public PrivateKeyAccess Access { get; }

        public static ICollection<PrivateKeyAccessRule> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<PrivateKeyAccessRule>>(json, JsonSerializerSettings);
        }

        private static JsonSerializerSettings JsonSerializerSettings => new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter(),
            }
        };

    }
}
