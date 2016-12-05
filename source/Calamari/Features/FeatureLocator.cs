using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Extensibility;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif
using Calamari.Util;

#if !NET40
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
#endif

namespace Calamari.Features
{
    public class FeatureLocator : IFeatureLocator
    {
        private readonly IGenericPackageExtractor extractor;
        private readonly IPackageStore packageStore;
        private readonly ICalamariFileSystem fileSystem;
        private readonly string customExtensionsPath;
        internal string BuiltInExtensionsPath = Path.Combine(Path.GetDirectoryName(typeof(FeatureLocator).GetTypeInfo().Assembly.FullLocalPath()), "Extensions");

        public FeatureLocator(IGenericPackageExtractor extractor, IPackageStore packageStore, ICalamariFileSystem fileSystem, string path = null)
        {
            this.extractor = extractor;
            this.packageStore = packageStore;
            this.fileSystem = fileSystem;

            customExtensionsPath = ExtensionsDirectory(path);
        }


        public FeatureExtension Locate(string name)
        {
            var type = Type.GetType(name, false);
            if (type != null && ValidateType(type))
            {
                return ToFeatureExtension(type);
            }

            var requestedType = new AssemblyQualifiedClassName(name);
            if (string.IsNullOrEmpty(requestedType?.AssemblyName))
                throw new InvalidOperationException($"Unable to determine feature from name `{name}`");

            if (TryLoadFromDirectory(BuiltInExtensionsPath, requestedType, out type) && ValidateType(type))
            {
                return ToFeatureExtension(type);
            }

            if (TryLoadFromDirectory(customExtensionsPath, requestedType, out type) && ValidateType(type))
            {
                return ToFeatureExtension(type);
            }

            if (!TryExtractFromPackageStagingDirectory(requestedType))
                return null;

            if (TryLoadFromDirectory(customExtensionsPath, requestedType, out type) && ValidateType(type))
            {
                return ToFeatureExtension(type);
            }

            throw new Exception(
                $"Extracted extension {requestedType.AssemblyName} but unable to get type {requestedType.ClassName}");
        }

        string ExtensionsDirectory(string path = null)
        {
            if (!string.IsNullOrEmpty(path))
                return path;

            var tentacleHome = Environment.GetEnvironmentVariable("TentacleHome");
            if (tentacleHome != null)
            {
                return Path.Combine(tentacleHome, "Extensions");
            }
            else
            {
                Log.Warn("Environment variable 'TentacleHome' has not been set.");
                return null;
            }
        }

        bool TryLoadFromDirectory(string path, AssemblyQualifiedClassName requestedClass, out Type type)
        {
            Log.Verbose($"Searching for feature in {path}");
            type = null;
            if (requestedClass.AssemblyName.Contains(Path.DirectorySeparatorChar))
                throw new InvalidOperationException("This provided assembly name contains the path seperator.");

            var dir = Path.Combine(path, requestedClass.AssemblyName);
            if (!Directory.Exists(dir))
            {
                return false;
            }

            var versioversionDirectories = Directory.EnumerateDirectories(dir).Select(filename =>
            {
                NuGetVersion version;
                NuGetVersion.TryParse(Path.GetFileName(filename), out version);
                return new
                {
                    filename,
                    version
                };
            }).Where(p => p.version != null);

            var fileName = (requestedClass.Version == null)
                ? versioversionDirectories.OrderByDescending(v => v.version).FirstOrDefault()?.filename
                : versioversionDirectories.FirstOrDefault(p => p.version.Equals(requestedClass.Version))?.filename;

            if (string.IsNullOrEmpty(fileName))
            {
                Log.Warn($"Unable to find a version of `{requestedClass.AssemblyName}` that is compatable with the requested feature.");
                return false;
            }

            foreach (var compatableFramework in CompatableFrameworks())
            {
                var frameworkFolder = Path.Combine(fileName, compatableFramework);
                if (Directory.Exists(frameworkFolder))
                {
                    try
                    {
                        var dllName = Path.Combine(frameworkFolder, $"{requestedClass.AssemblyName}.dll");
                        var assembly = CrossPlatform.LoadAssemblyFromDll(dllName);
                        type = assembly.GetType(requestedClass.ClassName, true);
                        Log.Verbose($"Loading feature {requestedClass} from {frameworkFolder}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }
            }

            Log.Warn($"Unable to find the a framework folder for assembly `{requestedClass.AssemblyName}` that is compatable with currently running framework. Valid frameworks are: {string.Join(", ", CompatableFrameworks())}");
            return false;
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

        FeatureExtension ToFeatureExtension(Type type)
        {
            return new FeatureExtension()
            {
                Feature = type,
                Details = GetFeatureAttribute(type)
            };
        }
        
        bool TryExtractFromPackageStagingDirectory(AssemblyQualifiedClassName assemblyName)
        {
            Log.Verbose($"Searching for feature in package staging directory.");

            var packages = packageStore.GetNearestPackages(assemblyName.AssemblyName, assemblyName.Version, 1).ToList();
            if (!packages.Any())
                return false;

            var pkg = packages.First();
            var assemblyDir = Path.Combine(customExtensionsPath, assemblyName.AssemblyName, pkg.Metadata.Version);

            fileSystem.EnsureDirectoryExists(assemblyDir);
            fileSystem.PurgeDirectory(assemblyDir, FailureOptions.ThrowOnFailure);
            Log.Verbose($"Extracting feature `{assemblyName}` to {assemblyDir}");
            extractor.Extract(pkg.FullPath, assemblyDir, true);
            var nugetLibDir = Path.Combine(assemblyDir, "lib");
            if (fileSystem.DirectoryExists(nugetLibDir))
            {
                foreach (var v in fileSystem.EnumerateFileSystemEntries(nugetLibDir))
                {
                    Directory.Move(v, Path.Combine(assemblyDir, Path.GetFileName(v)));
                }
                fileSystem.DeleteDirectory(nugetLibDir);
            }
            return true;
        }

        bool ValidateType(Type type)
        {
            if (!typeof(IFeature).IsAssignableFrom(type) && !typeof(IPackageDeploymentFeature).IsAssignableFrom(type))
                throw new InvalidOperationException(
                    $"Class `{type.FullName}` does not impliment IFeature or IPackageDeploymentFeature so is unable to be used in this operation.");

            return true;
        }

        FeatureAttribute GetFeatureAttribute(Type type)
        {
            var attribute = (FeatureAttribute) type.GetTypeInfo()
                .GetCustomAttributes(true)
                .FirstOrDefault(t => t is FeatureAttribute);

            if (attribute == null)
            {
                throw new InvalidOperationException(
                    $"Feature `{type.FullName}` does not have a FeatureAttribute attribute so is to be used in this operation.");
            }

            if (attribute.Module != null && attribute.Module.GetConstructors().All(c => c.GetParameters().Any()))
            {
                throw new InvalidOperationException(
                    $"Module `{attribute.Module.FullName}` does not have a default parameterless constructor.");
            }
            return attribute;
        }
    }
}