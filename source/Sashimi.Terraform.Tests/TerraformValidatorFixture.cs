using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Terraform.Validation;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class TerraformValidatorFixture
    {
        TerraformValidator? validator;
        IContainer? container;

        [SetUp]
        public void Setup()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<TerraformModule>();
            builder.RegisterModule<ServerModule>();
            container = builder.Build();
            validator = new TerraformValidator(container.Resolve<ICloudTemplateHandlerFactory>());
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        [Test]
        [TestCase(TerraformActionTypes.Apply)]
        [TestCase(TerraformActionTypes.Destroy)]
        public void Should_have_error_when_empty_inline_template(string actionType)
        {
            var context = new DeploymentActionValidationContext(actionType,
                new Dictionary<string, string>
                {
                    {KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline}
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(TerraformSpecialVariables.Action.Terraform.Template);
        }

        [Test]
        [TestCase(TerraformActionTypes.Apply)]
        [TestCase(TerraformActionTypes.Destroy)]
        public void Should_not_have_error_when_has_inline_template(string actionType)
        {
            var context = new DeploymentActionValidationContext(actionType,
                new Dictionary<string, string>
                {
                    {KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline},
                    {TerraformSpecialVariables.Action.Terraform.Template, "{}"}
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }

        [Test]
        [TestCase(TerraformActionTypes.Apply)]
        [TestCase(TerraformActionTypes.Destroy)]
        public void Should_have_error_when_inline_template_with_invalid_inline_variables(string actionType)
        {
            var template = @"variable ""test"" {
                type = ""string""
            }

            variable ""list"" {
                type = ""list""
            }

            variable ""map"" {
                type = ""map""
            }";
            var context = new DeploymentActionValidationContext(actionType,
                new Dictionary<string, string>
                {
                    {KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline},
                    {TerraformSpecialVariables.Action.Terraform.Template, template},
                    {
                        TerraformSpecialVariables.Action.Terraform.TemplateParameters,
                        @"{""list"": ""invalid list"", ""map"": ""invalid map""}"
                    }
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Properties");
        }

        [Test]
        [TestCase(TerraformActionTypes.Apply)]
        [TestCase(TerraformActionTypes.Destroy)]
        public void Should_have_no_error_when_inline_template_with_valid_inline_variables(string actionType)
        {
            var template = @"variable ""test"" {
                type = ""string""
            }

            variable ""list"" {
                type = ""list""
            }

            variable ""map"" {
                type = ""map""
            }";
            var context = new DeploymentActionValidationContext(actionType,
                new Dictionary<string, string>
                {
                    {KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline},
                    {TerraformSpecialVariables.Action.Terraform.Template, template},
                    {
                        TerraformSpecialVariables.Action.Terraform.TemplateParameters,
                        "{\"map\":\"{foo = \\\"bar\\\", baz = \\\"qux\\\"}\"}"
                    }
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }

        [Test]
        [TestCase(TerraformActionTypes.Apply)]
        [TestCase(TerraformActionTypes.Destroy)]
        public void Should_have_error_when_package_template_not_specified(string actionType)
        {
            var context = new DeploymentActionValidationContext(actionType,
                new Dictionary<string, string>
                {
                    {KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Package},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Packages");
        }

        [Test]
        [TestCase(TerraformActionTypes.Apply)]
        [TestCase(TerraformActionTypes.Destroy)]
        public void Should_have_no_error_when_package_template_is_specified(string actionType)
        {
            var context = new DeploymentActionValidationContext(actionType,
                new Dictionary<string, string>
                {
                    {KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Package},
                }, new List<PackageReference> {new PackageReference("packageId", "feedId")});
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void Should_have_error_when_azure_account_not_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.AzureAccount, Boolean.TrueString},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Octopus.Action.AzureAccount.Variable");
        }

        [Test]
        public void Should_have_no_error_when_azure_account_is_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.AzureAccount, Boolean.TrueString},
                    {"Octopus.Action.AzureAccount.Variable", "Myvariable"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void Should_have_error_when_aws_account_has_no_roles_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.ManagedAccount, TerraformSpecialVariables.AwsAccount},
                    {TerraformSpecialVariables.Action.Aws.AssumeRole, Boolean.TrueString},
                    {TerraformSpecialVariables.Action.Aws.AwsRegion, "MyRegion"},
                    {TerraformSpecialVariables.Action.Aws.AccountVariable, "MyVariable"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Select(_ => _.PropertyName).Should().Equal(
                TerraformSpecialVariables.Action.Aws.AssumedRoleArn,
                TerraformSpecialVariables.Action.Aws.AssumedRoleSession);
        }

        [Test]
        public void Should_have_no_error_when_aws_account_has_roles_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.ManagedAccount, TerraformSpecialVariables.AwsAccount},
                    {TerraformSpecialVariables.Action.Aws.AssumeRole, Boolean.TrueString},
                    {TerraformSpecialVariables.Action.Aws.AwsRegion, "MyRegion"},
                    {TerraformSpecialVariables.Action.Aws.AccountVariable, "MyVariable"},
                    {TerraformSpecialVariables.Action.Aws.AssumedRoleArn, "RoleArn"},
                    {TerraformSpecialVariables.Action.Aws.AssumedRoleSession, "RoleSession"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void Should_have_error_when_aws_account_with_no_region_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.ManagedAccount, TerraformSpecialVariables.AwsAccount},
                    {TerraformSpecialVariables.Action.Aws.AssumeRole, Boolean.FalseString},
                    {TerraformSpecialVariables.Action.Aws.AccountVariable, "MyVariable"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(TerraformSpecialVariables.Action.Aws.AwsRegion);
        }

        [Test]
        public void Should_have_no_error_when_aws_account_has_region_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.ManagedAccount, TerraformSpecialVariables.AwsAccount},
                    {TerraformSpecialVariables.Action.Aws.AssumeRole, Boolean.FalseString},
                    {TerraformSpecialVariables.Action.Aws.AwsRegion, "MyRegion"},
                    {TerraformSpecialVariables.Action.Aws.AccountVariable, "MyVariable"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void Should_have_error_when_aws_account_with_no_account_variable_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.ManagedAccount, TerraformSpecialVariables.AwsAccount},
                    {TerraformSpecialVariables.Action.Aws.AssumeRole, Boolean.FalseString},
                    {TerraformSpecialVariables.Action.Aws.AwsRegion, "MyRegion"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(TerraformSpecialVariables.Action.Aws.AccountVariable);
        }

        [Test]
        public void Should_have_no_error_when_aws_account_with_account_variable_specified()
        {
            var context = new DeploymentActionValidationContext(TerraformActionTypes.Plan,
                new Dictionary<string, string>
                {
                    {TerraformSpecialVariables.Action.Terraform.ManagedAccount, TerraformSpecialVariables.AwsAccount},
                    {TerraformSpecialVariables.Action.Aws.AssumeRole, Boolean.FalseString},
                    {TerraformSpecialVariables.Action.Aws.AwsRegion, "MyRegion"},
                    {TerraformSpecialVariables.Action.Aws.AccountVariable, "MyVariable"},
                }, new List<PackageReference>());
            
            var result = validator.TestValidate(context);
            
            result.IsValid.Should().BeTrue();
        }
    }
}