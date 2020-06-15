using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Terraform.Validation
{
    public class TerraformDeploymentActionValidation : IDeploymentActionValidator
    {
        readonly ICloudTemplateHandlerFactory cloudTemplateHandlerFactory;

        public TerraformDeploymentActionValidation(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
        {
            this.cloudTemplateHandlerFactory = cloudTemplateHandlerFactory;
        }
        
        public void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.Include(new TerraformValidator(cloudTemplateHandlerFactory));
        }
    }
}
