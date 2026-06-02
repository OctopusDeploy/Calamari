using System;
using System.Reflection;
using Calamari.Common.Commands;

namespace Calamari.Common.Util;

public static class TypeExtensionMethods
{
    public static string GetCommandNameFromAttribute(this Type type )
    {
        var attribute = type.GetCustomAttribute<CommandAttribute>();
        return attribute  is not null ? attribute.Name : string.Empty;
    }
}