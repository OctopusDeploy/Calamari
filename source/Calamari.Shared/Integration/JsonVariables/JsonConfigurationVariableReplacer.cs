using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octostache;

namespace Calamari.Integration.JsonVariables
{
    public class JsonConfigurationVariableReplacer : IJsonConfigurationVariableReplacer
    {
        public void ModifyJsonFile(string jsonFilePath, VariableDictionary variables)
        {
            var root = LoadJson(jsonFilePath);

            var map = new JsonUpdateMap();
            map.Load(root);
            map.Update(variables);

            SaveJson(jsonFilePath, root);
        }

        static JToken LoadJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                return new JObject();

            if (new FileInfo(jsonFilePath).Length == 0)
                return new JObject();

            using (var file = new FileStream(jsonFilePath, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(file))
            using (var json = new JsonTextReader(reader))
            {
                return JToken.ReadFrom(json);
            }
        }

        static void SaveJson(string jsonFilePath, JToken root)
        {
            using (var file = new FileStream(jsonFilePath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(file))
            using (var json = new JsonTextWriter(writer))
            {
                json.Formatting = Formatting.Indented;
                root.WriteTo(json);
            }
        }
    }

    public class JsonUpdateMap
    {
        private readonly IDictionary<string, Action<string>> map = new SortedDictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> path = new Stack<string>();
        private string key;

        public void Load(JToken json)
        {
            if (json.Type == JTokenType.Array)
            {
                MapArray(json.Value<JArray>(), true);
            }
            else
            {
                MapObject(json, true);
            }
        }

        private void MapObject(JToken j, bool first = false)
        {
            if (!first)
            {
                map[key] = t => j.Replace(JToken.Parse(t));
            }

            foreach (var property in j.Children<JProperty>())
            {
                MapProperty(property);
            }
        }

        private void MapToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    MapObject(token.Value<JObject>());
                    break;

                case JTokenType.Array:
                    MapArray(token.Value<JArray>());
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                    MapNumber(token);
                    break;

                case JTokenType.Boolean:
                    MapBool(token);
                    break;

                default:
                    MapDefault(token);
                    break;
            }
        }

        private void MapProperty(JProperty property)
        {
            PushPath(property.Name);
            MapToken(property.Value);
            PopPath();
        }

        private void MapDefault(JToken value)
        {
            map[key] = t => value.Replace(t == null ? null : JToken.FromObject(t));
        }

        private void MapNumber(JToken value)
        {
            map[key] = t =>
            {
                long longvalue;
                if (long.TryParse(t, out longvalue))
                {
                    value.Replace(JToken.FromObject(longvalue));
                    return;
                }
                double doublevalue;
                if (double.TryParse(t, out doublevalue))
                {
                    value.Replace(JToken.FromObject(doublevalue));
                    return;
                }
                value.Replace(JToken.FromObject(t));
            };
        }

        private void MapBool(JToken value)
        {
            map[key] = t =>
            {
                bool boolvalue;
                if (bool.TryParse(t, out boolvalue))
                {
                    value.Replace(JToken.FromObject(boolvalue));
                    return;
                }
                value.Replace(JToken.FromObject(t));
            };
        }

        private void MapArray(JContainer array, bool first = false)
        {
            if (!first)
            {
                map[key] = t => array.Replace(JToken.Parse(t));
            }

            for (var index = 0; index < array.Count; index++)
            {
                PushPath(index.ToString());
                MapToken(array[index]);
                PopPath();
            }
        }

        private void PushPath(string p)
        {
            path.Push(p);
            key = string.Join(":", path.Reverse());
        }

        private void PopPath()
        {
            path.Pop();
            key = string.Join(":", path.Reverse());
        }

        public void Update(VariableDictionary variables)
        {
            foreach (var name in variables.GetNames().Where(map.ContainsKey))
            {
                try
                {
                    map[name](variables.Get(name));
                }
                catch (Exception e)
                {
                    Log.WarnFormat("Unable to set value for {0}. The following error occurred: {1}", name, e.Message);
                }
            }
        }
    }
}
