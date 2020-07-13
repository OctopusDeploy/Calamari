using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes.Semaphores;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class ProcessFinderFixture
    {
        [Test]
        public void ProcessIsRunningReturnsTrueForCurrentProcess()
        {
            var processFinder = new ProcessFinder();
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var result = processFinder.ProcessIsRunning(currentProcess.Id, currentProcess.ProcessName);
            Assert.That(result, Is.True);
        }

        [Test]
        public void ProcessIsRunningReturnsFalseForNonExistantProcess()
        {
            var processFinder = new ProcessFinder();
            var result = processFinder.ProcessIsRunning(-1, Guid.NewGuid().ToString());
            Assert.That(result, Is.EqualTo(GetExpectedResult()));
        }

        private bool GetExpectedResult()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcesses();
                return false;
            }
            catch (NotSupportedException)
            {
                //not supported on FreeBSD. Probably a nicer way to do this.
                return true;
            }
        }
    }
}
