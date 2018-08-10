using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Hooks
{
    /// <summary>
    /// A bunch of extension methods used to merge StringDictionary objects.
    /// </summary>
    public static class StringDictionaryExtensions
    {
        
        /// <summary>
        /// Merges all the StringDictionary objects into one StringDictionary
        /// </summary>
        public static StringDictionary MergeDictionaries(this StringDictionary stringDictionary, params StringDictionary[] otherDictionary)
        {
            return (stringDictionary ?? new StringDictionary())
                .ToEnumerable()
                .Union(otherDictionary)
                .Where(dictionaries => dictionaries != null)
                .Aggregate(new StringDictionary(), (collection, current) =>
                {
                    foreach (var key in current.Keys)
                    {
                        collection.Add(key.ToString(), current[key.ToString()]?.ToString());
                    }

                    return collection;
                });
        }      
    }
}
