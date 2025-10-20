#if NET
using System;
using Calamari.ArgoCD.Conventions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class ArgoCDTaskLogExtensionMethodsTests
    {
        [Test]
        public void foo()
        {
            ILog log = new InMemoryLog();
            
            log.LogUnnamedAnnotationsInMultiSourceApplication()
        }
    }
}
#endif