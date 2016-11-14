using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari.Util;
#if !NET40
using Microsoft.Extensions.DependencyModel;
#endif

namespace Calamari.Features
{

    public interface IAssemblyLoader
    {
        IEnumerable<Type> Types { get; }
    }

    public class AssemblyLoader : IAssemblyLoader
    {
        private List<Assembly> assemblies { get; } = new List<Assembly>();

        public IEnumerable<Type> Types
        {
            get { return assemblies.SelectMany(a => a.GetTypes()); }
        }

        public void RegisterCompiled()
        {

#if !NET40
            LoadAssemblinesNetCore();
#else
             LoadAssembliesNet40();
#endif
        }



#if NET40
        void LoadAssembliesNet40()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterAssembly(assembly);
            }
        }
#else
        void LoadAssemblinesNetCore()
        {
            foreach (var compilationLibrary in DependencyContext.Default.RuntimeLibraries)
            {
                if (!compilationLibrary.Name.StartsWith("Calamari."))
                {
                    continue;
                }
                try
                {
                    RegisterAssembly(Assembly.Load(new AssemblyName(compilationLibrary.Name)));
                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message);
                }
            }
        }
#endif

        public void RegistryAssembly(string path)
        {
            if (!Directory.Exists(path))
                throw new InvalidOperationException($"Directory `{path}` does not appear to exist.");

            foreach (var file in Directory.EnumerateFiles(path, "*.dll"))
            {
                try
                {
                    var assembly = CrossPlatform.LoadAssemblyFromDll(file);
                    RegisterAssembly(assembly);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public void RegisterAssembly(Assembly assembly)
        {
            this.assemblies.Add(assembly);
        }

    }
}