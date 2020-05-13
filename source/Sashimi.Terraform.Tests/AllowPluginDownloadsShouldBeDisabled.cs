using Calamari.Terraform;
using NUnit.Framework;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Terraform.ActionHandler;
using Sashimi.Tests.Shared.Extensions;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class AllowPluginDownloadsShouldBeDisabled : BaseTest
    {
        protected void ArrangeTest(TestActionHandlerContext<Program> context)
        {
            //ExecuteAndReturnLogOutput<PlanCommand>(_ =>
           //                       _.Set(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, false.ToString()), "Simple")
            //                  .Should().Contain("init -no-color -get-plugins=false");
            var template = this.ReadResourceAsString("simple.tf");
            context.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
            context.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{}");
        }

        protected void AssertTest(IActionHandlerResult result)
        {
        }
    }
}