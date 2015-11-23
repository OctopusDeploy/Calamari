using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Integration.AppSettingsJson
{
    public class AppSettingsJsonGenerator : IAppSettingsJsonGenerator
    {
        const string KeyDelimiter = ":";
        
        public void Generate(string appSettingsFilePath, VariableDictionary variables)
        {
            var root = LoadJson(appSettingsFilePath);

            var names = variables.GetNames();
            names.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (name.StartsWith("Octopus", StringComparison.OrdinalIgnoreCase))
                    continue;

                SetValueRecursive(root, name, variables.Get(name));
            }

            SaveJson(appSettingsFilePath, root);
        }

        static JObject LoadJson(string appSettingsFilePath)
        {
            if (!File.Exists(appSettingsFilePath))
                return new JObject();

            if (new FileInfo(appSettingsFilePath).Length == 0)
                return new JObject();

            using (var reader = new StreamReader(appSettingsFilePath))
            using (var json = new JsonTextReader(reader))
            {
                return JObject.Load(json);
            }
        }

        static void SetValueRecursive(JObject currentObject, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var firstKey = name.IndexOf(KeyDelimiter, StringComparison.OrdinalIgnoreCase);
            if (firstKey > 0)
            {
                var key = name.Substring(0, firstKey);
                var remainder = name.Substring(firstKey + 1).Trim(':');

                var property = GetOrCreateProperty(currentObject, key);

                SetValueRecursive((JObject)property, remainder, value);
            }
            else
            {
                currentObject[name] = value;
            }
        }

        static JToken GetOrCreateProperty(JObject currentObject, string key)
        {
            JToken currentToken;
            if (currentObject.TryGetValue(key, StringComparison.InvariantCultureIgnoreCase, out currentToken))
            {
                if (currentToken is JObject == false)
                {
                    // This can happen if, for example, we have something like:
                    //  Foo = "Hello"
                    //  Foo:Bar = "World"
                    // In this case, what would Foo be set to - the string or the object? We go with the first value, the string. Since the keys are sorted first we get the shortest value
                    return currentToken;
                }
            }
            else
            {
                currentObject[key] = currentToken = new JObject();
            }
            return currentToken;
        }

        static void SaveJson(string appSettingsFilePath, JObject root)
        {
            using (var writer = new StreamWriter(appSettingsFilePath))
            using (var json = new JsonTextWriter(writer))
            {
                json.Formatting = Formatting.Indented;
                root.WriteTo(json);
            }
        }
    }
}
