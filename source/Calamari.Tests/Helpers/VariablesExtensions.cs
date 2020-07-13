using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Tests.Helpers
{
    public static class VariablesExtensions
    {
        public static void Save(this IVariables variables, string filename)
        {
            var dictionary = variables.ToDictionary(x => x.Key, x => x.Value);
            File.WriteAllText(filename, JsonConvert.SerializeObject(dictionary));
        }
    }
}