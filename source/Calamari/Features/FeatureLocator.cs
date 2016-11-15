using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NET40
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
#endif

using Calamari.Shared.Features;

namespace Calamari.Features
{
    public class FeatureLocator : IFeatureLocator
    {  
        public Type GetFeatureType(string name)
        {
            var handler = FeatureHandlers.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (handler == null)
            {
                throw new InvalidOperationException($"Unable to find feature with name '{name}'. Either it was not listed as a dependency or it failed to load due to an invalid FeatureHandler.");
            }
            return handler.Feature;
        }
        
        public FeatureLocator(IAssemblyLoader assemblyLoader)
        {
            LoadTypes(assemblyLoader.Types);
        }

        private List<IFeatureHandler> FeatureHandlers;

        void LoadTypes(IEnumerable<Type>  types)
        {
            FeatureHandlers = types.Where(typeof(IFeatureHandler).IsAssignableFrom)
               .Select(feature =>
                {
#if NET40
                    if (feature.IsInterface)
                        return null;
#else

                   if (feature.GetTypeInfo().IsInterface)
                        return null;
#endif
                   try
                   {
                       if (!feature.GetConstructors().Any(ctr => ctr.GetParameters().Any()))
                       {
                           var handler = (IFeatureHandler) Activator.CreateInstance(feature);
                           if (!typeof(IFeature).IsAssignableFrom(handler.Feature))
                           {
                                Log.WarnFormat("The the type described by feature handler `{0}` does not impliment IFeature so will be ignored. This may be a fatal problem if it is required for this operation.", feature.FullName);
                               return null;
                           }

                           return handler;
                       }

                       Log.WarnFormat("The feature handler `{0}` does not have a parameterless constructor so will be unable to be instantiated. This may be a fatal problem if it is required for this operation.", feature.FullName);
                       return null;
                   }
                   catch (Exception ex)
                   {
                       Log.WarnFormat("Error loading feature `{0}` so it will be ignored. This may be a fatal problem if it is required for this operation.{1}{2}", feature.FullName, Environment.NewLine, ex.ToString());
                   }
                   return null;
               }).Where(f => f != null).ToList();
        }
    }
}