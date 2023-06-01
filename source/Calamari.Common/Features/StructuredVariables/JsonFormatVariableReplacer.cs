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
                JToken root;
                StringExtensions.LineEnding? lineEnding = null;
                Encoding? encoding = null;
                
                if (!fileSystem.FileExists(filePath))
                {
                    root = new JObject();
                }
                else
                {
                     var fileText = fileSystem.ReadFile(filePath, out encoding);
                     if (fileText.Length == 0)
                     {
                         root = new JObject();
                     }
                     else
                     {
                         root = JToken.Parse(fileText);
                         lineEnding = fileText.GetMostCommonLineEnding();
                     }
                }
                
                var map = new JsonUpdateMap(log);
                map.Load(root);
                map.Update(variables);

                fileSystem.OverwriteFile(filePath,
                                         textWriter =>
                                         {
                                             textWriter.NewLine = lineEnding == StringExtensions.LineEnding.Unix ? "\n" : "\r\n";
                                             var jsonWriter = new JsonTextWriter(textWriter);
                                             jsonWriter.Formatting = Formatting.Indented;
                                             root.WriteTo(jsonWriter);
                                         }, encoding);
            }
            catch (JsonReaderException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }
        }
    }
}