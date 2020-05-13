using Calamari.Terraform;
using NUnit.Framework;
using Sashimi.Terraform.ActionHandler;
using Sashimi.Tests.Shared.Extensions;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class TerraformTests : BaseTest
    {
        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            TestActionHandler<TerraformPlanActionHandler, Program>(context =>
                {
                    var template = this.ReadResourceAsString("simple.tf");
                    context.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                    context.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{}");
                }, result =>
                {
                    Assert.AreEqual(0, result.ExitCode);
                }
            );
        }
    }
}