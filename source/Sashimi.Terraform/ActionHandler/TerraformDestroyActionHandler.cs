using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Terraform.ActionHandler
{
    public class TerraformDestroyActionHandler : TerraformActionHandler
    {
        public TerraformDestroyActionHandler(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
            : base(cloudTemplateHandlerFactory)
        {
        }

        public override string Id => TerraformActionTypes.Destroy;
        public override string Name => "Destroy Terraform resources";
        public override string Description => "Destroys Terraform resources";
        public override string ToolCommand => "destroy-terraform";
    }
}