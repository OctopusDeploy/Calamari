using System;
using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresDotNet45Attribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (!ScriptingEnvironment.IsNet45OrNewer())
            {
                Assert.Ignore("Requires .NET 4.5");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}