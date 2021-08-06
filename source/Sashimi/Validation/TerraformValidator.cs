using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Newtonsoft.Json.Linq;
using Octopus.CoreParsers.Hcl;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Terraform.ActionHandler;
using Sashimi.Terraform.CloudTemplates;
using Sprache;

namespace Sashimi.Terraform.Validation
{
    class TerraformValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        const string DefaultTemplate = "{}";
        readonly ICloudTemplateHandlerFactory cloudTemplateHandlerFactory;

        public TerraformValidator(ICloudTemplateHandlerFactory cloudTemplateHandlerFactory)
        {
            this.cloudTemplateHandlerFactory = cloudTemplateHandlerFactory;

            When(a => (a.ActionType == TerraformActionTypes.Apply || a.ActionType == TerraformActionTypes.Destroy) && !IsTemplateFromPackage(a.Properties),
                 () =>
                 {
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(TerraformSpecialVariables.Action.Terraform.Template,
                                           "Please provide the Terraform template.");
                     RuleFor(a => a.Properties)
                         .Must(a => !ValidationVariables(a).Any())
                         .WithMessage(a =>
                                          $"The variable(s) could not be parsed: {string.Join(", ", ValidationVariables(a.Properties))}.");
                 });

            RuleFor(a => a.Packages)
                .Must(packages => packages.Any(p => p.IsPrimaryPackage && !string.IsNullOrWhiteSpace(p.PackageId) && !string.IsNullOrWhiteSpace(p.FeedIdOrName?.Value)))
                .WithMessage("Please provide the Terraform template package.")
                .When(a => (a.ActionType == TerraformActionTypes.Apply || a.ActionType == TerraformActionTypes.Destroy) && IsTemplateFromPackage(a.Properties));

            AddAzureAccountRules(this);
            AddAwsAccountRules(this);
        }

        static void AddAzureAccountRules(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            bool AzureAccountSelected(DeploymentActionValidationContext a)
            {
                return a.Properties.ContainsKey(TerraformSpecialVariables.Action.Terraform.AzureAccount)
                       && a.Properties[TerraformSpecialVariables.Action.Terraform.AzureAccount].ToLower() == "true";
            }

            validator.RuleFor(a => a.Properties)
                     .MustHaveProperty("Octopus.Action.AzureAccount.Variable", "Please specify an Azure account variable.")
                     .When(AzureAccountSelected);
        }

        static void AddAwsAccountRules(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            bool AwsAccountSelected(DeploymentActionValidationContext a)
            {
                return a.Properties.ContainsKey(TerraformSpecialVariables.Action.Terraform.ManagedAccount)
                       && a.Properties[TerraformSpecialVariables.Action.Terraform.ManagedAccount] == TerraformSpecialVariables.AwsAccount;
            }

            bool ChosenToAssumeRole(DeploymentActionValidationContext a)
            {
                return a.Properties.ContainsKey(TerraformSpecialVariables.Action.Aws.AssumeRole)
                       && bool.TrueString.Equals(a.Properties[TerraformSpecialVariables.Action.Aws.AssumeRole], StringComparison.OrdinalIgnoreCase);
            }

            bool ChosenToUseInstanceRole(DeploymentActionValidationContext a)
            {
                return a.Properties.ContainsKey(TerraformSpecialVariables.Action.Aws.UseInstanceRole)
                       && bool.TrueString.Equals(a.Properties[TerraformSpecialVariables.Action.Aws.UseInstanceRole], StringComparison.OrdinalIgnoreCase);
            }

            validator.When(x => AwsAccountSelected(x) && ChosenToAssumeRole(x),
                           () =>
                           {
                               validator.RuleFor(a => a.Properties)
                                        .MustHaveProperty(TerraformSpecialVariables.Action.Aws.AssumedRoleArn,
                                                          "Please provide the assumed role ARN.");

                               validator.RuleFor(a => a.Properties)
                                        .MustHaveProperty(TerraformSpecialVariables.Action.Aws.AssumedRoleSession,
                                                          "Please provide the assumed role session name.");
                           });

            validator.RuleFor(a => a.Properties)
                     .MustHaveProperty(TerraformSpecialVariables.Action.Aws.AccountVariable, "Please specify an AWS account variable.")
                     .When(x => AwsAccountSelected(x) && !ChosenToUseInstanceRole(x));

            validator.RuleFor(a => a.Properties)
                     .MustHaveProperty(TerraformSpecialVariables.Action.Aws.AwsRegion, "Please specify the AWS region.")
                     .When(AwsAccountSelected);
        }

        /// <summary>
        /// Find maps and lists in the supplied variables, and attempt to parse them as HCL or JSON
        /// structures. Any that fail are returned in the list.
        /// </summary>
        /// <param name="properties">JSON string containing the raw strings for the data structures</param>
        /// <returns>The names of any variables that failed to be parsed</returns>
        IEnumerable<string> ValidationVariables(IReadOnlyDictionary<string, string> properties)
        {
            // If we switched from an inline script to a package script, we may have variables
            // defined. So don't process anything if we are currently deploying a package.
            if (IsTemplateFromPackage(properties))
                return Enumerable.Empty<string>();

            var variables = properties.ContainsKey(TerraformSpecialVariables.Action.Terraform.TemplateParameters) ? properties[TerraformSpecialVariables.Action.Terraform.TemplateParameters] ?? DefaultTemplate : DefaultTemplate;
            var template = properties.ContainsKey(TerraformSpecialVariables.Action.Terraform.Template) ? properties[TerraformSpecialVariables.Action.Terraform.Template] ?? DefaultTemplate : DefaultTemplate;

            var templateHandler = cloudTemplateHandlerFactory.GetHandler(TerraformConstants.CloudTemplateProviderId, template);

            if (templateHandler == null)
                return new[] { TerraformSpecialVariables.Action.Terraform.Template };

            var metadata = templateHandler.ParseTypes(template);

            var invalidProperties = JObject.Parse(variables)
                                           .Properties()
                                           .Select(prop => new { Prop = prop, Type = TerraformVariableFileGenerator.GetPropertyType(metadata, prop.Name) })
                                           // Don't validate empty values
                                           .Where(propDetails => !string.IsNullOrWhiteSpace(propDetails.Prop.Value.ToString()))
                                           // Only validate raw values
                                           .Where(propDetails => propDetails.Type?.StartsWith(TerraformDataTypes.RawPrefix) ?? false)
                                           // Don't validate values with variable substitution
                                           .Where(propDetails => !propDetails.Prop.Value.ToString().Contains("#{"))
                                           // Limit the results to those that fail validation
                                           .Where(propDetails =>
                                                  {
                                                      if (templateHandler is TerraformHclCloudTemplateHandler)
                                                      {
                                                          if (propDetails.Type == TerraformDataTypes.RawMap)
                                                              return !HclParser.MapValue.End().TryParse(propDetails.Prop.Value.ToString()).WasSuccessful;

                                                          return !HclParser.ListValue.End().TryParse(propDetails.Prop.Value.ToString()).WasSuccessful;
                                                      }

                                                      try
                                                      {
                                                          if (propDetails.Type == TerraformDataTypes.RawMap)
                                                          {
                                                              JObject.Parse(propDetails.Prop.Value.ToString());
                                                              return false;
                                                          }

                                                          JArray.Parse(propDetails.Prop.Value.ToString());
                                                          return false;
                                                      }
                                                      catch (Exception)
                                                      {
                                                          return true;
                                                      }
                                                  })
                                           // Generate an error message
                                           .Select(propDetails => propDetails.Prop.Name + $" (expected a {propDetails.Type?.Replace(TerraformDataTypes.RawPrefix, "")})")
                                           .ToList();

            return invalidProperties;
        }

        static bool IsTemplateFromPackage(IReadOnlyDictionary<string, string> properties)
        {
            return properties.TryGetValue(KnownVariables.Action.Script.ScriptSource, out var scriptSource) && scriptSource == KnownVariableValues.Action.Script.ScriptSource.Package;
        }
    }
}