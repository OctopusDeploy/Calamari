using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Terraform.ActionHandler
{
    public class TerraformPlanActionHandler : TerraformActionHandler
    {
        public TerraformPlanActionHandler(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
            : base(cloudTemplateHandlerFactory)
        {
        }

        public override string Id => TerraformActionTypes.Plan;
        public override string Name => "Plan to apply a Terraform template";
        public override string Description => "Plans the creation of a Terraform deployment";
        public override string ToolCommand => "plan-terraform";
    }
}