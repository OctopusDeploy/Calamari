using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class IVersionConverter : JsonConverter
    {
        public override bool CanWrite => true;
        public override bool CanRead => true;

        public override bool CanConvert(Type objectType) => objectType == typeof(IVersion);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var version = value as IVersion ?? throw new Exception("Type must implement IVersion to use this converter.");
            var outputVersion = new { Version = version.ToString(), Format = version.Format.ToString() };

            serializer.Serialize(writer, outputVersion);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var versionString = jsonObject["Version"].Value<string>();
            var versionFormat = (VersionFormat) Enum.Parse(typeof(VersionFormat), jsonObject["Format"].Value<string>());

            var version = VersionFactory.CreateVersion(versionString, versionFormat);

            return version;
        }
    }

}