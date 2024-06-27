using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace Calamari.Kubernetes.Helm
{
    public class RawValuesToYamlConverter
    {
        /// <summary>
        /// Converts collections of dot-notation properties into a structured YAM string
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static string Convert(List<KeyValuePair<string, object>> values)
        {
            var structuredObject = new Dictionary<string, object>();
            
            foreach (var v in values.OrderBy(k => k.Key))
            {
                AddEntry(v.Key, v.Value, structuredObject);
            }

            return new Serializer().Serialize(structuredObject);
        }
        
        public static string Convert(Dictionary<string, object> values)
        {
            return Convert(values.ToList());
        }

        private static void AddEntry(string dotname, object value, Dictionary<string, object> structuredObject)
        {
            var keys = dotname.Split('.');
            var subDict = structuredObject;
            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                
                if (i == keys.Length - 1) // Final dot sub-property, this is where the value lives!
                {
                    if (!subDict.ContainsKey(key))
                    {
                        subDict[key] = value;
                    }
                    else
                    {
                        // Another property has come this way before, lets make it a list
                        SetValueAsList(value, key, subDict);  
                    }
                }
                else
                {
                    if (!subDict.ContainsKey(key)) 
                    {
                        // Another property has come this way before, lets make it an object
                        var innerDict = new Dictionary<string, object>();
                        subDict[key] = innerDict;
                        subDict = innerDict;
                    }
                    else
                    {
                        subDict = (Dictionary<string, object>) subDict[key];
                    }
                }
            }
        }

        private static void SetValueAsList(object value, string key, Dictionary<string, object> structuredObject)
        {
            var existing = structuredObject[key];
            if (existing.GetType() == typeof(List<object>))
            {
                ((List<object>) existing).Add(value);
            }
            else
            {
                structuredObject[key] = new List<object>() {existing, value};
            }
        }
    }
}