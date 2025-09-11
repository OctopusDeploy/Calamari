using System;
using System.Reflection;
using Nuke.Common;

namespace Calamari.Build.Utilities;

public class ParameterFromPasswordStore : ParameterAttribute
{
    public override object GetValue(MemberInfo member, object instance)
    {
        var value = SecretManager.GetValue(this);
        return value ?? base.GetValue(member, instance)!;
    }

    public string SecretReference { get; init; } = null!;
}
