﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Kubernetes
{
    public static class SpecialVariables
    {
        public const string ClusterUrl = "Octopus.Action.Kubernetes.ClusterUrl";
        public const string AksClusterName = "Octopus.Action.Kubernetes.AksClusterName";
        public const string EksClusterName = "Octopus.Action.Kubernetes.EksClusterName";
        public const string Namespace = "Octopus.Action.Kubernetes.Namespace";
        public const string SkipTlsVerification = "Octopus.Action.Kubernetes.SkipTlsVerification";

        public static class Helm
        {
            public const string ReleaseName = "Octopus.Action.Helm.ReleaseName";
            public const string Namespace = "Octopus.Action.Helm.Namespace";
            public const string KeyValues = "Octopus.Action.Helm.KeyValues";
            public const string YamlValues = "Octopus.Action.Helm.YamlValues";
            public const string RecreatePods = "Octopus.Action.Helm.RecreatePods";
            public const string ResetValues = "Octopus.Action.Helm.ResetValues";
            public const string CustomHelmExecutable = "Octopus.Action.Helm.CustomHelmExecutable";
            public const string Timeout = "Octopus.Action.Helm.Timeout";
            public const string TillerNamespace = "Octopus.Action.Helm.TillerNamespace";
            public const string TillerTimeout = "Octopus.Action.Helm.TillerTimeout";
            
            public static class Packages
            {
                public const string CustomHelmExePackageKey = "HelmExe";
                public static string ValuesFilePath(string key)
                {
                    return $"Octopus.Action.Package[{key}].ValuesFilePath";
                }
            }
        }
    }
}
