using System;
using Calamari.Common.Features.Scripts;

namespace Calamari.Terraform
{
    static class TerraformSpecialVariables
    {
        public const string JsonTemplateFile = "template.tf.json";
        public const string HclTemplateFile = "template.tf";
        public const string JsonVariablesFile = "terraform.tfvars.json";
        public const string HclVariablesFile = "terraform.tfvars";

        public static class Action
        {
            public static class Terraform
            {
                public const string GoogleCloudAccount = "Octopus.Action.Terraform.GoogleCloudAccount";
                public const string PlanJsonOutput = "Octopus.Action.Terraform.PlanJsonOutput";
                public const string PlanJsonChangesAdd = "TerraformPlanJsonAdd";
                public const string PlanJsonChangesChange = "TerraformPlanJsonChange";
                public const string PlanJsonChangesRemove = "TerraformPlanJsonRemove";
                public const string Template = "Octopus.Action.Terraform.Template";
                public const string TemplateParameters = "Octopus.Action.Terraform.TemplateParameters";
                public const string RunAutomaticFileSubstitution = "Octopus.Action.Terraform.RunAutomaticFileSubstitution";
                public const string ManagedAccount = "Octopus.Action.Terraform.ManagedAccount";
                public const string AzureAccount = "Octopus.Action.Terraform.AzureAccount";
                public const string AllowPluginDownloads = "Octopus.Action.Terraform.AllowPluginDownloads";
                public const string PluginsDirectory = "Octopus.Action.Terraform.PluginsDirectory";
                public const string TemplateDirectory = "Octopus.Action.Terraform.TemplateDirectory";
                public const string FileSubstitution = "Octopus.Action.Terraform.FileSubstitution";
                public const string Workspace = "Octopus.Action.Terraform.Workspace";
                public const string CustomTerraformExecutable = "Octopus.Action.Terraform.CustomTerraformExecutable";
                public const string AttachLogFile = "Octopus.Action.Terraform.AttachLogFile";
                public const string AdditionalInitParams = "Octopus.Action.Terraform.AdditionalInitParams";
                public const string AdditionalActionParams = "Octopus.Action.Terraform.AdditionalActionParams";
                public const string VarFiles = "Octopus.Action.Terraform.VarFiles";
                public const string AWSManagedAccount = "Octopus.Action.Terraform.ManagedAccount";
                public const string AzureManagedAccount = "Octopus.Action.Terraform.AzureAccount";
                public const string PlanOutput = "TerraformPlanOutput";
                public const string PlanDetailedExitCode = "TerraformPlanDetailedExitCode";
                public const string EnvironmentVariables = "Octopus.Action.Terraform.EnvVariables";
            }
        }

        public static class Script
        {
            public static readonly string Syntax = "Octopus.Action.Script.Syntax";
            public static readonly string ScriptBody = "Octopus.Action.Script.ScriptBody";
            public static readonly string ScriptFileName = "Octopus.Action.Script.ScriptFileName";
            public static readonly string ScriptParameters = "Octopus.Action.Script.ScriptParameters";
            public static readonly string ScriptSource = "Octopus.Action.Script.ScriptSource";

            public static string ScriptBodyBySyntax(ScriptSyntax syntax) => "Octopus.Action.Script.ScriptBody[" + syntax.ToString() + "]";

            public static class ScriptSourceOptions
            {
                public const string Package = "Package";
                public const string Inline = "Inline";
            }
        }

        public static class Calamari
        {
            public static readonly string TerraformCliPath = "Octopus.Calamari.TerraformCliPath";
        }
        
        public static class Packages
        {
            public static readonly string PackageId = "Octopus.Action.Package.PackageId";
        }
    }
}