using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Integration.JsonVariables
{
    public class JsonConfigurationVariableReplacer : IJsonConfigurationVariableReplacer
    {
        const string KeyDelimiter = ":";
        
        public void ModifyJsonFile(string jsonFilePath, VariableDictionary variables)
        {
            var root = LoadJson(jsonFilePath);

            var names = variables.GetNames();
            names.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (name.StartsWith("Octopus", StringComparison.OrdinalIgnoreCase))
                    continue;

                SetValueRecursive(root, name, name, variables.Get(name));
            }

            SaveJson(jsonFilePath, root);
        }

        static JObject LoadJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                return new JObject();

            if (new FileInfo(jsonFilePath).Length == 0)
                return new JObject();

            using (var reader = new StreamReader(jsonFilePath))
            using (var json = new JsonTextReader(reader))
            {
                return JObject.Load(json);
            }
        }

        static void SetValueRecursive(JObject currentObject, string name, string fullName, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var firstKey = name.IndexOf(KeyDelimiter, StringComparison.OrdinalIgnoreCase);
            if (firstKey > 0)
            {
                var key = name.Substring(0, firstKey);
                var remainder = name.Substring(firstKey + 1).Trim(':');

                var property = GetOrCreateProperty(currentObject, key, fullName);

                if (property != null)
                {
                    SetValueRecursive((JObject)property, remainder, fullName, value);
                }
            }
            else
            {
                var settings = (
                    from objectValue in currentObject.Values()
                    let key = objectValue.Path
                    where key != null
                    where string.Equals(key, name, StringComparison.InvariantCultureIgnoreCase)
                    select objectValue).ToList();

                if (!settings.Any()) return;

                foreach (var setting in settings)
                {
                    currentObject[setting.Path] = value;
                }
            }
        }

        static JObject GetOrCreateProperty(JObject currentObject, string key, string fullName)
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
                    Log.WarnFormat("Unable to set value for {0}. The property at {1} is a {2}.", fullName, currentToken.Path, currentToken.Type);
                    return null;
                }
            }
            else
            {
                currentObject[key] = currentToken = new JObject();
            }
            return (JObject) currentToken;
        }

        static void SaveJson(string jsonFilePath, JObject root)
        {
            using (var writer = new StreamWriter(jsonFilePath))
            using (var json = new JsonTextWriter(writer))
            {
                json.Formatting = Formatting.Indented;
                root.WriteTo(json);
            }
        }
    }
}
