using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari.Extensibility.Features;
using Calamari.Integration.Processes;
using Calamari.Util;
#if !NET40
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
#endif

namespace Calamari.Features
{
    public class FeatureLocator : IFeatureLocator
    {
        
        private readonly string basePath;

        public FeatureLocator(string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                basePath = Path.Combine(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.FullLocalPath()), "Extensions");
            }
            basePath = path;
        }


        public Type LoadFromDirectory(string assemblyName, string className)
        {
            if (assemblyName.Contains(Path.DirectorySeparatorChar))
                throw new InvalidOperationException("This provided assembly name contains the path seperator.");

            var dir = Path.Combine(basePath, assemblyName);
            if (!Directory.Exists(dir))
            {
                throw new Exception($"The assembly `{assemblyName}` appears to be missing in the extensions directory `{dir}`");
            }

            foreach (var compatableFramework in CompatableFrameworks())
            {
                var frameworkFolder = Path.Combine(dir, compatableFramework);
                if (Directory.Exists(frameworkFolder))
                {
                    try
                    {
                        var dllName = Path.Combine(frameworkFolder, $"{assemblyName}.dll");
                        var assembly = CrossPlatform.LoadAssemblyFromDll(dllName);
                        return assembly.GetType(className);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            throw new Exception($"Unable to find the a framework folder for assembly `{assemblyName}` that is compatable with currently running framework. Valid frameworks are: {string.Join(", ", CompatableFrameworks())}");
        }



        IEnumerable<string> CompatableFrameworks()
        {
#if !NET40
            yield return "netstandard1.6";
#else
            yield return "net451";
            yield return "net40";
#endif
        }

        

        public Type Locate(string name)
        {
            var type = Type.GetType(name, false);
            if (type != null && ValidateType(type))
                return type;

            var requestedType = RequestedClass.ParseFromAssemblyQualifiedName(name);
            if(requestedType == null || string.IsNullOrEmpty(requestedType.AssemblyName))
                throw new InvalidOperationException($"Unable to determine feature from name `{name}`");

            type = LoadFromDirectory(requestedType.AssemblyName, requestedType.ClassName);

            if (type != null && ValidateType(type))
                return type;

            throw new InvalidOperationException($"Unable to determine feature from name `{name}`");
        }


        bool ValidateType(Type type)
        {
            if (!typeof(IFeature).IsAssignableFrom(type) && !typeof(IPackageDeploymentFeature).IsAssignableFrom(type))
                throw new InvalidOperationException($"Class `{type.FullName}` does not impliment IFeature or IPackageDeploymentFeature so is unable to be used in this operation.");

            if (FeatureDetails(type) == null)
            {
                throw new InvalidOperationException($"Feature `{type.FullName}` does not have a FeatureAttribute attribute so is to be used in this operation.");
            }

            return true;
        }

        FeatureAttribute FeatureDetails(Type type)
        {
            var attribute = type.GetTypeInfo()
                   .GetCustomAttributes(true)
                   .FirstOrDefault(t => t is FeatureAttribute);
            return  attribute as FeatureAttribute;
        }
    }
}