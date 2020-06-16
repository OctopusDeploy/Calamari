using System;
using System.Collections.Generic;
using System.Linq;
using Assent;
using NUnit.Framework;
using Server.Extensibility.Tests;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(TerraformModule);
    }
}