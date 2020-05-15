using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Terraform.ActionHandler
{
    public class TerraformPlanDestroyActionHandler : TerraformActionHandler
    {
        public TerraformPlanDestroyActionHandler(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
            : base(cloudTemplateHandlerFactory)
        {
        }

        public override string Id => TerraformActionTypes.PlanDestroy;
        public override string Name => "Plan a Terraform destroy";
        public override string Description => "Plans the destruction of Terraform resources";
        public override string ToolCommand => "destroyplan-terraform";
    }
}