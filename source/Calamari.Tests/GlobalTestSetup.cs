using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;
using NUnit.Framework;

namespace Calamari.Tests
{
    // using statements

    [SetUpFixture]
    public class GlobalTestSetup
    {
        [OneTimeSetUp]
        public void EnableAllSecurityProtocols()
        {
            // Enabling of TLS1.2 happens on Calamari.exe startup in main,
            // however this will ensure it is applied during Unit Tests which will bypass the main entrypoint
            SecurityProtocols.EnableAllSecurityProtocols();
        }
    }
}
