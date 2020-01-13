using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresHelm2Attribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest test)
        {
            throw new System.NotImplementedException();
        }

        public void AfterTest(ITest test)
        {
            
        }

        public ActionTargets Targets { get; }
    }
}