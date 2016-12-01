using System.Reflection;

#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif

namespace Calamari.Features
{
    public class AssemblyQualifiedClassName
    {
        public AssemblyQualifiedClassName(string assemblyQualifiedName)
        {
            original = assemblyQualifiedName;
            var library = assemblyQualifiedName.IndexOf(',');
            if (library == -1)
            {
                this.ClassName = assemblyQualifiedName;
                return;
            }

            this.ClassName = assemblyQualifiedName.Substring(0, library).Trim();
            var assemblyName = new AssemblyName(assemblyQualifiedName.Substring(library+1).Trim());
            if (assemblyName.Version != null)
            {
                Version = NuGetVersion.Parse(assemblyName.Version.ToString());
            }
            AssemblyName = assemblyName.Name;
        }

        public NuGetVersion Version { get; private set; }
        public string AssemblyName { get; private set; }
        public string ClassName { get; private set; }

        private readonly string original;
        public override string ToString()
        {
            return original;
        }
    }
}
