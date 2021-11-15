using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Calamari.Tests.Fixtures.Conventions
{
    public static class AppDomainScanner
    {
        static readonly Lazy<Type[]> CalamariTypesLazy;

        static AppDomainScanner()
        {

            CalamariTypesLazy = new Lazy<Type[]>(ScanForCalamariTypes);
        }

        public static IReadOnlyCollection<Type> CalamariTypes => CalamariTypesLazy.Value;

        static Type[] ScanForCalamariTypes()
        {
            var calamariAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(IsCalamariAssembly);
            var types = calamariAssemblies
                .SelectMany(a =>
                    {
                        try
                        {
                            return a.DefinedTypes.ToArray();
                        }
                        catch (ReflectionTypeLoadException)
                        {
                            return Array.Empty<Type>();
                        }
                        catch (TypeLoadException)
                        {
                            return Array.Empty<Type>();
                        }
                    })
                .ToArray();

            return types;
        }

        static bool IsCalamariAssembly(Assembly assembly)
        {
            if (assembly.IsDynamic) return false;
            return assembly.GetName().Name?.StartsWith("Calamari") ?? false;
        }
    }
}