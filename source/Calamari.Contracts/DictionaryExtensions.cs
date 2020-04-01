using System;
using System.Collections.Generic;

namespace Calamari.Extensions
{
    public static class DictionaryExtensions
    {
        public static void Add(this IDictionary<string, string> collection, IDictionary<string, string> items)
        {
            if (items == null)
            {
                return; 
            }

            foreach (var obj in items)
            {
                collection[obj.Key] = obj.Value;
            }
        }
    }
}
