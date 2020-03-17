using System;
using System.IO;

namespace Sashimi.Tests.Shared.Extensions
{
    public static class EmbeddedResourceExtensions
    {
        public static string ReadResourceAsString(this object test, string postfix)
        {
            var name = test.GetType().Namespace + "." + postfix;
            using (var stream = test.GetType().Assembly.GetManifestResourceStream(name))
            using (var sr = new StreamReader(stream ?? throw new Exception($"Could not find the resource {name}")))
                return sr.ReadToEnd();
        }
    }
}
