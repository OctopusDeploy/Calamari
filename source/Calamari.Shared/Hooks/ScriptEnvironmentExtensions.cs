using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Hooks
{
    /// <summary>
    /// A bunch of extension methods used to merge StringDictionary objects,
    /// or the StringDictionary objects held by collections of IScriptEnvironment.
    /// </summary>
    public static class ScriptEnvironmentExtensions
    {
        /// <summary>
        /// Merges all the IScriptEnvironment objects into one StringDictionary
        /// </summary>
        public static StringDictionary MergeDictionaries(this IEnumerable<IScriptEnvironment> environmentHooks)
        {
            return environmentHooks?
                       .Where(hook => hook != null)
                       .Aggregate(new StringDictionary(), (collection, current) =>
                       {
                           foreach (var key in current.EnvironmentVars.Keys)
                           {
                               collection.Add(key.ToString(), current.EnvironmentVars[key.ToString()].ToString());
                           }

                           return collection;
                       }) ?? new StringDictionary();
        }

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
                        collection.Add(key.ToString(), current[key.ToString()].ToString());
                    }

                    return collection;
                });
        }

        /// <summary>
        /// Merges one StringDictionary into a collection of another
        /// </summary>
        public static StringDictionary MergeDictionaries(this IEnumerable<IScriptEnvironment> environmentHooks, IScriptEnvironment scriptEnvironment)
        {
            if (environmentHooks == null && scriptEnvironment == null)
            {
                return new StringDictionary();
            }

            if (environmentHooks == null)
            {
                return scriptEnvironment.EnvironmentVars;
            }

            if (scriptEnvironment == null)
            {
                return environmentHooks.MergeDictionaries();
            }

            return scriptEnvironment
                // treat the single value as an enumerable
                .ToEnumerable()
                // union the two enumerable collections of IScriptEnvironment
                .Map(environmentHooks.Union)
                // finally merge into a single sting dictionary
                .MergeDictionaries();
        }

        /// <summary>
        /// Merges a collection of IScriptEnvironment objects together with an additional StringDictionary
        /// </summary>
        public static StringDictionary MergeDictionaries(this IEnumerable<IScriptEnvironment> environmentHooks, StringDictionary scriptEnvironment)
        {
            if (environmentHooks == null && scriptEnvironment == null)
            {
                return new StringDictionary();
            }

            if (environmentHooks == null)
            {
                return scriptEnvironment;
            }

            if (scriptEnvironment == null)
            {
                return environmentHooks.MergeDictionaries();
            }

            return environmentHooks.MergeDictionaries().MergeDictionaries(scriptEnvironment);
        }
    }
}
