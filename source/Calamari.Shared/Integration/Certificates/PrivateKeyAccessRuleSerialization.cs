#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System;
using System.Collections.Generic;
using Calamari.FullFrameworkTools.Contracts.WindowsCertStore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.Integration.Certificates
{
    public static class PrivateKeyAccessRuleSerialization {

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
#endif