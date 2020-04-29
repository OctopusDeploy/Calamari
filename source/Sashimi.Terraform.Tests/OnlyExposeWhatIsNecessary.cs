using System;
using System.Linq;
using Assent;
using NUnit.Framework;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary
    {
        [Test]
        public void ServerExtensionsShouldMinimiseWhatIsExposed()
        {
            var assembly = typeof(TerraformModule).Assembly;

            var publicThings = assembly.GetExportedTypes()
                .Select(t => t.FullName);
            
            this.Assent(string.Join(Environment.NewLine, publicThings));
        }
    }
}