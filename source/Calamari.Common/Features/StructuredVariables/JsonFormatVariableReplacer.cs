using System;
using System.Text;
using Calamari.Common.Plumbing.Extensions;
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
                var json = LoadJson(filePath);
                var map = new JsonUpdateMap(log);
                map.Load(json.root);
                map.Update(variables);

                fileSystem.OverwriteFile(filePath,
                                         textWriter =>
                                         {
                                             textWriter.NewLine = json.lineEnding == StringExtensions.LineEnding.Unix ? "\n" : "\r\n";
                                             var jsonWriter = new JsonTextWriter(textWriter);
                                             jsonWriter.Formatting = Formatting.Indented;
                                             json.root.WriteTo(jsonWriter);
                                         }, json.encoding);
            }
            catch (JsonReaderException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }
        }

        (JToken root, StringExtensions.LineEnding? lineEnding, Encoding? encoding) LoadJson(string jsonFilePath)
        {
            if (!fileSystem.FileExists(jsonFilePath))
                return (new JObject(), null, null);

            var fileText = fileSystem.ReadFile(jsonFilePath, out var encoding);
            if (fileText.Length == 0)
                return (new JObject(), null, null);

            return (JToken.Parse(fileText), fileText.GetMostCommonLineEnding(), encoding);
        }
    }
}