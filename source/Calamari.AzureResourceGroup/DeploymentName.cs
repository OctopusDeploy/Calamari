using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Calamari.AzureResourceGroup
{
    public static class DeploymentName
    {
        public static string FromStepName(string? stepName)
        {
            var deploymentName = stepName ?? string.Empty;
            deploymentName = deploymentName.ToLower();
            deploymentName = Regex.Replace(deploymentName, "\\s", "-");
            deploymentName = new string(deploymentName.Select(x => (char.IsLetterOrDigit(x) || x == '-') ? x : '-').ToArray());
            deploymentName = Regex.Replace(deploymentName, "-+", "-");
            deploymentName = deploymentName.Trim('-', '/');
            // Azure Deployment Names can only be 64 characters == 31 chars + "-" (1) + Guid (32 chars)
            deploymentName = deploymentName.Length <= 31 ? deploymentName : deploymentName.Substring(0, 31);
            deploymentName = deploymentName + "-" + Guid.NewGuid().ToString("N");
            return deploymentName;
        }
    }
}