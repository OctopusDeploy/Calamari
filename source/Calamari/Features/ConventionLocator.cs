using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Shared.Convention;
using Calamari.Util;

namespace Calamari.Features
{
    public class ConventionLocator
    {
        IDictionary<string, Type> conventions;

        public ConventionLocator(IAssemblyLoader assemblyLoader)
        {
            LoadTypes(assemblyLoader.Types);
        }
        
        void LoadTypes(IEnumerable<Type> loadedTypes)
        {
            conventions = loadedTypes
                .Where(typeof(IConvention).IsAssignableFrom)
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
                        .FirstOrDefault(t => t is ConventionMetadataAttribute);
                    if (attribute != null)
                        return new {Convention = f, ((ConventionMetadataAttribute) attribute).Name};

                    Log.WarnFormat(
                        "Convention `{0}` does not have a ConventionMetadata attribute so it will be ignored." +
                        " This may be a fatal problem if it is required for this operation.", f.FullName);
                    return null;
                }).Where(f => f != null).ToDictionary(t => t.Name, t => t.Convention); //, StringComparer.InvariantCultureIgnoreCase);
        }

        public Type Locate(string name)
        {
            if (!conventions.ContainsKey(name))
            {
                return null;
            }

            return conventions[name];
        }
    }
}