using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Common.Features.StructuredVariables
{
    public class JsonFormatVariableReplacer : IJsonFormatVariableReplacer
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public JsonFormatVariableReplacer(ICalamariFileSystem fileSystem, ILog log)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public string FileFormatName => "JSON";

        public bool TryModifyFile(string filePath, IVariables variables)
        {
            JToken root;
            try
            {
                root = LoadJson(filePath);
            }
            catch (JsonReaderException)
            {
                // File was not valid JSON.
                return false;
            }

            var map = new JsonUpdateMap(log);
            map.Load(root);
            map.Update(variables);

            SaveJson(filePath, root);
            return true;
        }

        JToken LoadJson(string jsonFilePath)
        {
            if (!fileSystem.FileExists(jsonFilePath))
                return new JObject();

            var fileContents = fileSystem.ReadFile(jsonFilePath);
            if (fileContents.Length == 0)
                return new JObject();

            return JToken.Parse(fileContents);
        }

        void SaveJson(string jsonFilePath, JToken root)
        {
            fileSystem.OverwriteFile(jsonFilePath, root.ToString());
        }
    }
}