using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Common.Features.StructuredVariables
{
    public class JsonFormatVariableReplacer : IFileFormatVariableReplacer
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public JsonFormatVariableReplacer(ICalamariFileSystem fileSystem, ILog log)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public string FileFormatName => StructuredConfigVariablesFileFormats.Json;

        public bool IsBestReplacerForFileName(string fileName)
        {
            return fileName.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase);
        }
        
        public void ModifyFile(string filePath, IVariables variables)
        {
            try
            {
                JToken root = LoadJson(filePath);
                var map = new JsonUpdateMap(log);
                map.Load(root);
                map.Update(variables);

                SaveJson(filePath, root);
            }
            catch (JsonReaderException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }
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