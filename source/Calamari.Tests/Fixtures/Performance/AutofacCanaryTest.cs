using Autofac;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using NUnit.Framework;
using System;
using System.Linq;


namespace Calamari.Tests.Fixtures.Performance
{
    /// <summary>
    /// This test has been added to catch any potential performance issues with the Autofac
    /// code used to register and construct the objects that make up a typical calamari
    /// execution. If this test fails it is an indication that there *might* be performance
    /// issues in this code path. There are no hard and fast rules as to what an acceptable
    /// level of performance is though, so make a judgement call if the thresholds are reasonable.
    /// </summary>
    [TestFixture]
    class AutofacCanaryTest
    {
        private const int Iterations = 1000;
        private const int GroupThreshold = 10;
        /// <summary>
        /// Acceptable difference between the two measurements in milliseconds. This is somewhat abitary,
        /// but if this test starts to fail it *could* be an indication of performance issues.
        /// </summary>
        private const int Threshold = Iterations * GroupThreshold;

        /// <summary>
        /// When running a single instance of the test there is a greater performance difference.
        /// </summary>
        private const int IndividualThreshold = 500;        
        
        [Test]
        public void AutofacRegistrationPerformance()
        {
            // Run through the process of creating an autofac container, registering an external
            // module (the test module in this case), and creating the objects that make up
            // a typical calamari execution path.
            var autofacTime = new TimerClass().DoTimedAction(() =>
            {
                using (var container =
                    Calamari.Program.BuildContainer(new[] {"help", "run-script", "--extensions=Tests"}))
                {
                    container.Resolve<Calamari.Program>();
                }
            }, Iterations);

            // Do the same as above, but without auofac.
            var regularTime = new TimerClass().DoTimedAction(() =>
                {
                    new Calamari.Program(
                        "name", 
                        "version", 
                        new string[] { }, 
                        new RunScriptCommand(new CalamariVariableDictionary(), new CombinedScriptEngine()),
                        new HelpCommand(Enumerable.Empty<ICommandMetadata>()));
                }, Iterations);

            Assert.LessOrEqual(autofacTime, regularTime + Threshold);
        }

        [Test]
        public void InidividualAutofacRegistrationPerformance()
        {
            // Run through the process of creating an autofac container, registering an external
            // module (the test module in this case), and creating the objects that make up
            // a typical calamari execution path.
            var autofacTime = new TimerClass().DoTimedAction(() =>
            {
                using (var container =
                    Calamari.Program.BuildContainer(new[] { "help", "run-script", "--extensions=Tests" }))
                {
                    container.Resolve<Calamari.Program>();
                }
            }, 1);

            // Do the same as above, but without auofac.
            var regularTime = new TimerClass().DoTimedAction(() =>
            {
                new Calamari.Program(
                    "name",
                    "version",
                    new string[] { },
                    new RunScriptCommand(new CalamariVariableDictionary(), new CombinedScriptEngine()),
                    new HelpCommand(Enumerable.Empty<ICommandMetadata>()));
            }, 1);

            Assert.LessOrEqual(autofacTime, regularTime + IndividualThreshold);
        }
    }

    class TimerClass
    {
        public long DoTimedAction(Action action, int iterations)
        {
            var start = DateTime.Now.Ticks;

            for (var i = 0; i < iterations; ++i)
            {
                action();
            }

            return (DateTime.Now.Ticks - start) / TimeSpan.TicksPerMillisecond;
        }
    }
}
