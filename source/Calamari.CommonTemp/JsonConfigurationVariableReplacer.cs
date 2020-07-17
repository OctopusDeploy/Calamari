using System.IO;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.CommonTemp
{
    internal class JsonConfigurationVariableReplacer : IJsonConfigurationVariableReplacer
    {
        public void ModifyJsonFile(string jsonFilePath, IVariables variables)
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
                json.Formatting = Newtonsoft.Json.Formatting.Indented;
                root.WriteTo(json);
            }
        }
    }
}