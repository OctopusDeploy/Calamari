using System;
using System.IO;

namespace Calamari.Common.Features.Processes
{
    public static class EmbeddedResource
    {
        public static string ReadEmbeddedText(string name)
        {
            var thisType = typeof(EmbeddedResource);
            using (var resource = thisType.Assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(resource))
            {
                return reader.ReadToEnd();
            }
        }
    }
}