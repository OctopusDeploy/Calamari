using System.Collections.Generic;

namespace Calamari.Common.Aws
{
    public interface IAwsEnvironmentVariables
    {
        Dictionary<string, string> EnvironmentVars { get; }
    }
}