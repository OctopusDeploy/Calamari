namespace Calamari.Terraform
{
    public static class TerraformSpecialVariables
    {
        public const string TerraformScript = "octopus-terraform.ps1";
        public const string HclTemplateFile = "template.tf";
        public const string JsonTemplateFile = "template.tf.json";
        public const string JsonVariablesFile = "terraform.tfvars.json";
        public const string HclVariablesFile = "terraform.tfvars";
        public const string AwsAccount = "AWS";
        
        public static class Action
        {
            public static class SubstituteInFiles
            {
                public const string Enabled = "Octopus.Action.SubstituteInFiles.Enabled";
                public const string Targets = "Octopus.Action.SubstituteInFiles.TargetFiles";
                public const string EnableNoMatchWarning = "Octopus.Action.SubstituteInFiles.EnableNoMatchWarning";
            }
            
            public static class Terraform
            {           
                public const string Template = "Octopus.Action.Terraform.Template";
                public const string TemplateParameters = "Octopus.Action.Terraform.TemplateParameters";
                public const string ManagedAccount = "Octopus.Action.Terraform.ManagedAccount";                                
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
                public const string PlanOutput = "TerraformPlanOutput";                
            }
        }

        public static class Calamari
        {
            public static readonly string TerraformCliPath = "Octopus.Calamari.TerraformCliPath";
        }
    }
}