using System;
using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Calamari.Tests.Fixtures
{
    public class RequiresDotNetCoreAttribute: NUnitAttribute, IApplyToTest
    {
        static bool IsNetCore()
        {
            #if NETCORE
                return true;
            #else
                return false;
            #endif
        }

        public void ApplyToTest(Test test)
        {
            if (!IsNetCore())
            {
                test.RunState = RunState.Skipped;
                test.Properties.Set(PropertyNames.SkipReason, "Requires dotnet core");
            }
        }
    }
}