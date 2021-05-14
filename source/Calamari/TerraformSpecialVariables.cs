using System;

namespace Calamari.Terraform
{
    public static class TerraformSpecialVariables
    {
        public static class Action
        {
            public static class Terraform
            {
                public const string AWSManagedAccount = "Octopus.Action.Terraform.ManagedAccount";
                public const string AzureManagedAccount = "Octopus.Action.Terraform.AzureAccount";
                public const string GoogleCloudAccount = "Octopus.Action.Terraform.GoogleCloudAccount";
                public const string AllowPluginDownloads = "Octopus.Action.Terraform.AllowPluginDownloads";
                public const string PluginsDirectory = "Octopus.Action.Terraform.PluginsDirectory";
                public const string TemplateDirectory = "Octopus.Action.Terraform.TemplateDirectory";
                public const string FileSubstitution = "Octopus.Action.Terraform.FileSubstitution";
                public const string RunAutomaticFileSubstitution = "Octopus.Action.Terraform.RunAutomaticFileSubstitution";
                public const string Workspace = "Octopus.Action.Terraform.Workspace";
                public const string CustomTerraformExecutable = "Octopus.Action.Terraform.CustomTerraformExecutable";
                public const string AttachLogFile = "Octopus.Action.Terraform.AttachLogFile";
                public const string AdditionalInitParams = "Octopus.Action.Terraform.AdditionalInitParams";
                public const string AdditionalActionParams = "Octopus.Action.Terraform.AdditionalActionParams";
                public const string VarFiles = "Octopus.Action.Terraform.VarFiles";
                public const string PlanOutput = "TerraformPlanOutput";
                public const string PlanDetailedExitCode = "TerraformPlanDetailedExitCode";
                public const string EnvironmentVariables = "Octopus.Action.Terraform.EnvVariables";
            }
        }
    }
}