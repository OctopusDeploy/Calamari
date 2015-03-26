using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Integration.ConfigurationTransforms
{
        public class XmlConfigTransformDefinition
        {
            public XmlConfigTransformDefinition(string definition)
            {
                if (definition.Contains("=>"))
                {
                    Advanced = true;
                    var separators = new[] {"=>"};
                    var parts = definition.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                    TransformPattern = parts[0].Trim();
                    SourcePattern = parts[1].Trim();

                    if (TransformPattern.StartsWith("*."))
                    {
                        Wildcard = true;
                        TransformPattern = TransformPattern.Remove(0, 2);
                    }

                    if (SourcePattern.StartsWith("*."))
                    {
                        Wildcard = true;
                        SourcePattern = SourcePattern.Remove(0, 2);
                    }
                }
                else
                {
                    TransformPattern = definition;
                }

            }

            public string TransformPattern { get; private set; }
            public string SourcePattern { get; private set; }
            public bool Wildcard { get; private set; }
            public bool Advanced { get; private set; }


        }
}