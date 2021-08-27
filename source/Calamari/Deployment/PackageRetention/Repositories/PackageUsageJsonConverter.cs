using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class PackageUsageJsonConverter : JsonConverter
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();

           // var obj = JObject.Load(reader);
           // obj.GetValue("PackageUsage");
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(PackageUsageJsonConverter).IsAssignableFrom(objectType);
        }
    }
}