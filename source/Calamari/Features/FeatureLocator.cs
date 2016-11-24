using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari.Extensibility.Features;
using Calamari.Util;
#if !NET40
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
#endif

namespace Calamari.Features
{
    public class FeatureLocator : IFeatureLocator
    {
        public FeatureLocator(IAssemblyLoader assemblyLoader)
        {
            LoadTypes(assemblyLoader.Types);
        }
       

        IDictionary<string, Type> features;


        void LoadTypes(IEnumerable<Type> loadedTypes)
        {
            features = loadedTypes
                .Where(typeof(IFeature).IsAssignableFrom)
                .Select(f =>
                {
#if NET40
                    if (f.IsInterface)
                        return null;
#else

                    if (f.GetTypeInfo().IsInterface)
                        return null;
#endif
                    var attribute = f.GetTypeInfo()
                        .GetCustomAttributes(true)
                        .FirstOrDefault(t => t is FeatureAttribute);
                    if (attribute != null)
                        return new {Convention = f, ((FeatureAttribute) attribute).Name};

                    Log.WarnFormat(
                        "Feature `{0}` does not have a FeatureAttribute attribute so it will be ignored." +
                        " This may be a fatal problem if it is required for this operation.", f.FullName);
                    return null;
                })
                .Where(f => f != null)
                .ToDictionary(t => t.Name, t => t.Convention, StringComparer.OrdinalIgnoreCase);
        }

        public Type Locate(string name)
        {
            if (!features.ContainsKey(name))
            {
                throw new InvalidOperationException($"Unable to find feature with name '{name}'.");
            }
            return features[name];
        }       
    }
}