using System;

namespace Sashimi.Terraform
{
    /// <summary>
    /// Constants to represents the variables passed down by the UI
    /// </summary>
    static class TerraformSpecialVariables
    {
        public const string HclTemplateFile = "template.tf";
        public const string JsonTemplateFile = "template.tf.json";
        public const string JsonVariablesFile = "terraform.tfvars.json";
        public const string HclVariablesFile = "terraform.tfvars";
        public const string AwsAccount = "AWS";
        
        public static class Action
        {
            public static class Terraform
            {           
                public const string Template = "Octopus.Action.Terraform.Template";
                public const string TemplateParameters = "Octopus.Action.Terraform.TemplateParameters";
                public const string RunAutomaticFileSubstitution = "Octopus.Action.Terraform.RunAutomaticFileSubstitution";
                public const string ManagedAccount = "Octopus.Action.Terraform.ManagedAccount";                                
                public const string AzureAccount = "Octopus.Action.Terraform.AzureAccount";                 
            }

            public static class Aws
            {
                public const string AccountVariable = "Octopus.Action.AwsAccount.Variable";
                public const string UseInstanceRole = "Octopus.Action.AwsAccount.UseInstanceRole";
                public const string AwsRegion = "Octopus.Action.Aws.Region";
                public const string AssumeRole = "Octopus.Action.Aws.AssumeRole";
                public const string AssumedRoleArn = "Octopus.Action.Aws.AssumedRoleArn";
                public const string AssumedRoleSession = "Octopus.Action.Aws.AssumedRoleSession";
            }
        }

        public static class Calamari
        {
            public static readonly string TerraformCliPath = "Octopus.Calamari.TerraformCliPath";
        }
    }
}