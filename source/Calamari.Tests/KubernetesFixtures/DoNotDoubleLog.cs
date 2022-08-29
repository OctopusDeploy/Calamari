using System;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.KubernetesFixtures
{
    class DoNotDoubleLog : InMemoryLog
    {
        protected override void StdErr(string message)
        {
        }

        protected override void StdOut(string message)
        {
        }
    }
}