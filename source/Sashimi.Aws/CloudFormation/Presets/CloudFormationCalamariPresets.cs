using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Aws.CloudFormation.Presets
{
    class CloudFormationCalamariPresets
    {
        public static void TemplatesAndParameters(IActionAndTargetScopedVariables variables, ICalamariCommandBuilder builder)
        {
            var templateInPackage = variables.Get(AwsSpecialVariables.Action.Aws.TemplateSource) == AwsSpecialVariables.Action.Aws.TemplateSourceOptions.Package;
            if (templateInPackage)
                builder.WithStagedPackageArgument();

            AddTemplateParametersArgument(variables, templateInPackage, builder);
            AddTemplateArgument(variables,templateInPackage, builder);
        }

        static void AddTemplateParametersArgument(IActionAndTargetScopedVariables variables, bool templateInPackage, ICalamariCommandBuilder builder)
        {
            if (templateInPackage)
            {
                var templateParameterFileName = variables.Get(AwsSpecialVariables.Action.Aws.CloudFormation.TemplateParametersRaw);
                if (!string.IsNullOrEmpty(templateParameterFileName))
                    builder.WithArgument("templateParameters", templateParameterFileName);
            }
            else
            {
                var processedTemplateParametersJson = variables.Get(AwsSpecialVariables.Action.Aws.CloudFormation.TemplateParameters);
                if (!string.IsNullOrEmpty(processedTemplateParametersJson))
                    builder.WithDataFileAsArgument("templateParameters", processedTemplateParametersJson, "parameters.json");
            }
        }

        static void AddTemplateArgument(IActionAndTargetScopedVariables variables, bool templateInPackage, ICalamariCommandBuilder builder)
        {
            var template = variables.Get(AwsSpecialVariables.Action.Aws.CloudFormation.Template); // Same variable as above, but this time contains the template contents instead of the filename

            if (templateInPackage)
            {
                if (template == null)
                    throw new KnownDeploymentFailureException("The cloud formation template filename has not been supplied");
                builder.WithArgument("template", variables.Get(AwsSpecialVariables.Action.Aws.CloudFormation.Template));
            }
            else
            {
                if (template == null)
                    throw new KnownDeploymentFailureException("The inline cloud formation template has not been supplied");
                builder.WithDataFileAsArgument("template", template, "template.json");
            }
        }
    }
}