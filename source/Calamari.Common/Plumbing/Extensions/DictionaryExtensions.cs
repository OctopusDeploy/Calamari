using System;
using System.Collections.Generic;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class DictionaryExtensions
    {
        public static void AddRange(this IDictionary<string, string> collection, IDictionary<string, string> items)
        {
            if (items == null)
                return;

            foreach (var obj in items)
                collection[obj.Key] = obj.Value;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> collection, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (collection.TryGetValue(key, out TValue value))
                return value;

            var newValue = valueFactory(key);
            collection[key] = newValue;
            return newValue;
        }
    }
}