using System;

namespace Sashimi.Terraform
{
    class TerraformActionTypes
    {
        public const string Apply = "Octopus.TerraformApply";
        public const string Destroy = "Octopus.TerraformDestroy";            
        public const string Plan = "Octopus.TerraformPlan";            
        public const string PlanDestroy = "Octopus.TerraformPlanDestroy"; 
    }
}