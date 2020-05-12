using System;
using Octopus.CoreUtilities;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Terraform.CloudTemplates;

namespace Sashimi.Terraform.ActionHandler
{
    public class TerraformApplyActionHandler : TerraformActionHandler
    {
        public override string Id => TerraformActionTypes.Apply;
        public override string Name => "Apply a Terraform template";
        public override string Description => "Applies a Terraform template";
        public override string ToolCommand => "apply-terraform";

        public TerraformApplyActionHandler(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
            : base(cloudTemplateHandlerFactory)
        {
        }
    }

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

    /// <summary>
    /// The action handler that prepares a Calamari script execution with
    /// the path set to include the Terraform CLI.
    /// </summary>
    public abstract class TerraformActionHandler : IActionHandler
    {
        public static readonly CalamariFlavour CalamariTerraform = new CalamariFlavour("Calamari.Terraform");

        readonly ICloudTemplateHandlerFactory cloudTemplateHandlerFactory;
        
        public TerraformActionHandler(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
        {
            this.cloudTemplateHandlerFactory = cloudTemplateHandlerFactory;
        }

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public string? Keywords => null;
        public abstract string ToolCommand { get; }
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] {ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Terraform};

        public IActionHandlerResult Execute(IActionHandlerContext context)
        {
            var builder = context.CalamariCommand(CalamariTerraform, ToolCommand);

            if (context.DeploymentTargetType.SelectValueOr(targetType => targetType != DeploymentTargetType.Ssh, true))
            {
                builder.WithTool(TerraformTools.TerraformCli);
            }

            var isInPackage = KnownVariableValues.Action.Script.ScriptSource.Package.Equals(context.Variables.Get(KnownVariables.Action.Script.ScriptSource), StringComparison.OrdinalIgnoreCase);
            if (isInPackage)
            {
                builder.WithStagedPackageArgument();
            }
            else
            {
                var template = context.Variables.Get(TerraformSpecialVariables.Action.Terraform.Template);
                var templateParametersRaw = context.Variables.GetRaw(TerraformSpecialVariables.Action.Terraform.TemplateParameters);

                if (string.IsNullOrEmpty(template))
                {
                    throw new KnownDeploymentFailureException("No template supplied");
                }

                if (string.IsNullOrEmpty(templateParametersRaw))
                {
                    throw new KnownDeploymentFailureException("No template parameters applied");
                }

                var templateHandler = cloudTemplateHandlerFactory.GetHandler(TerraformConstants.CloudTemplateProviderId, template);

                if (templateHandler == null)
                    throw new KnownDeploymentFailureException("Could not parse Terraform template as JSON or HCL");

                var templateFormat = templateHandler is TerraformJsonCloudTemplateHandler ? TerraformTemplateFormat.Json : TerraformTemplateFormat.Hcl;
                var metadata = templateHandler.ParseTypes(template);
                var templateParameters = TerraformVariableFileGenerator.ConvertStringPropsToObjects(templateFormat, context.Variables, templateParametersRaw, metadata);

                builder.WithDataFileNoBom(
                        template,
                        templateFormat == TerraformTemplateFormat.Json ? TerraformSpecialVariables.JsonTemplateFile : TerraformSpecialVariables.HclTemplateFile
                    )
                    .WithDataFileNoBom(
                        templateParameters,
                        templateFormat == TerraformTemplateFormat.Json ? TerraformSpecialVariables.JsonVariablesFile : TerraformSpecialVariables.HclVariablesFile
                    );
            }

            return builder.Execute();
        }
    }
}