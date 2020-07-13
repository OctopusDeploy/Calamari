using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class DeploymentVariables
    {
        public static string Id = "Octopus.Deployment.Id";

        public static class Tenant
        {
            public static string Id = "Octopus.Deployment.Tenant.Id";
            public static string Name = "Octopus.Deployment.Tenant.Name";
        }
    }
}