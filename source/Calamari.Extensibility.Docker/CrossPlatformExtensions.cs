using System;

namespace Calamari.Extensibility.Docker
{
    public static class CrossPlatformExtensions
    {

#if NET40
        public static Type GetTypeInfo(this Type type)
        {
            return type;
        }
#endif

    }
}
