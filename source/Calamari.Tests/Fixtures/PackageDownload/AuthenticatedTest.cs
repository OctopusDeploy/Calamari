using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AuthenticatedTest : Attribute, ITestAction
    {
        readonly string _feedUsernameVariable;
        readonly string _feedPasswordVariable;

        public AuthenticatedTest(string feedUsernameVariable, string feedPasswordVariable)
        {
            _feedUsernameVariable = feedUsernameVariable;
            _feedPasswordVariable = feedPasswordVariable;
        }

        public void BeforeTest(TestDetails testDetails)
        {
            if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(_feedUsernameVariable)))
            {
                Assert.Ignore("The authenticated feed tests were skipped because the " + _feedUsernameVariable + " and " +_feedPasswordVariable + " environment variables are not set.");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        public ActionTargets Targets { get; private set; }
    }
}
