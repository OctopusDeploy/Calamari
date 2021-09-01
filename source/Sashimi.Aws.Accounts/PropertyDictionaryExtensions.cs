using System;
using System.Collections.Generic;

static class PropertyDictionaryExtensions
{
    public static bool ContainsPropertyWithValue(this IDictionary<string, string> dictionary, string key)
    {
        if (!dictionary.ContainsKey(key))
            return false;

        string value = dictionary[key];
        return value != null && !string.IsNullOrEmpty(value);
    }
}