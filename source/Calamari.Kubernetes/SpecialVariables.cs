using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Kubernetes
{
    public static class SpecialVariables
    {
        public const string ClusterUrl = "Octopus.Action.Kubernetes.ClusterUrl";
        public const string Namespace = "Octopus.Action.Kubernetes.Namespace";
        public const string SkipTlsVerification = "Octopus.Action.Kubernetes.SkipTlsVerification";

        public static class Helm
        {
            public const string Install = "Octopus.Action.Helm.Install";
            public const string ReleaseName = "Octopus.Action.Helm.ReleaseName";
            public const string KeyValues = "Octopus.Action.Helm.KeyValues";

            public static class Packages
            {
                public static string PerformVariableReplace(string key)
                {
                    return $"Octopus.Action.Package[{key}].PerformVariableReplace";
                }

                public static string ValuesFilePath(string key)
                {
                    return $"Octopus.Action.Package[{key}].ValuesFilePath";
                }
            }
        }
    }
}
