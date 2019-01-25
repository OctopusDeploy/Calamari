using System;
using System.Collections.Generic;

namespace Calamari.Extensions
{
    public static class DictionaryExtensions
    {
        public static void MergeDictionaries(this IDictionary<string, string> collection, IDictionary<string, string> items)
        {
<<<<<<< HEAD
            if (items == null)
            {
                return;
            }

=======
>>>>>>> Removing usages of StringDictionary
            foreach (var obj in items)
            {
                collection[obj.Key] = obj.Value;
            }
        }
    }
}
