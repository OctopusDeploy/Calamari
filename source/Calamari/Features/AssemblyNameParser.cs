using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Calamari.Features
{
    public class RequestedClass
    {
        public static RequestedClass ParseFromAssemblyQualifiedName(string assemblyQualifiedName)
        {
            var c = new RequestedClass();

            var library = assemblyQualifiedName.Split(',');
            if (library.Length == 0)
            {
                return null;
            }
            c.ClassName = library[0].Trim();

            if (library.Length > 1)
            {
                c.AssemblyName = library[1].Trim();
            }
            return c;
        }

        public string AssemblyName { get; private set; }
        public string ClassName { get; private set; }
    }
}
