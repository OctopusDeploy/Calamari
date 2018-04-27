using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Calamari.Plugin
{
    public static class ScriptEnvironmentExtensions
    {
        /// <summary>
        /// Merges all the StringDictionary objects into one StringDictionary
        /// </summary>
        public static StringDictionary MergeDictionaries(this IEnumerable<IScriptEnvironment> environmentPlugins)
        {
            return environmentPlugins.Aggregate(new StringDictionary(), (collection, current) =>
            {
                foreach (var key in current.EnvironmentVars.Keys)
                {
                    collection.Add(key.ToString(), current.EnvironmentVars[key.ToString()].ToString());
                }

                return collection;
            });
        }
    }
}
