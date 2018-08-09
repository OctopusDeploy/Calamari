using Autofac;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using NUnit.Framework;
using System;
using System.Linq;
using Calamari.Modules;


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
        private readonly ICommandLocator CommandLocator = new CommandLocator();

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
            // Run a process that replicates the old pipeline
            new TimerClass().DoTimedAction(DoOldCalamariExecutionPipeline, 1);

            // Run it again to get the performance after any optimisations gained by scanning
            // assemblies the first time around.
            var regularTime = new TimerClass().DoTimedAction(DoOldCalamariExecutionPipeline, Iterations);

            // Run through the process of creating an autofac container, registering an external
            // module (the test module in this case), and creating the objects that make up
            // a typical calamari execution path.
            var autofacTime = new TimerClass().DoTimedAction(() =>
            {
                using (var container =
                    Calamari.Program.BuildContainer(new[] { "help", "run-script", "--extensions=Calamari.Tests" }))
                {
                    container.Resolve<Calamari.Program>();
                }
            }, Iterations);

            Warn.If(autofacTime > regularTime + Threshold, 
                $"Expected {autofacTime} to be less than or equal to {regularTime + Threshold}");
        }

        /// <summary>
        /// This test is designed to measure the one off cost of using autofac. Since running calamari
        /// is a one off operation, this is more applicable than the AutofacRegistrationPerformance() test,
        /// which shares the cost of initial initiation over multiple loops.
        ///
        /// However this test is fundamentally flawed in that if any autofac code was loaded by any other test
        /// the performance difference between the two code paths is trivial. We don't control the
        /// order of test execution, so don't rely on this test passing in the CI builds as indication
        /// of anything.
        ///
        /// Running this test locally and individually is a good test though, and can be used with other
        /// tracing tools to get a better understanding of the bottlenecks.
        /// </summary>
        [Test]
        public void InidividualAutofacRegistrationPerformance()
        {
            // Run a process that replicates the old pipeline
            new TimerClass().DoTimedAction(DoOldCalamariExecutionPipeline, 1);

            // Run it again to get the performance after any optimisations gained by scanning
            // assemblies the first time around.
            var regularTime = new TimerClass().DoTimedAction(DoOldCalamariExecutionPipeline, 1);

            // Run through the process of creating an autofac container, registering an external
            // module (the test module in this case), and creating the objects that make up
            // a typical calamari execution path.
            var autofacTime = new TimerClass().DoTimedAction(() =>
            {
                using (var container =
                    Calamari.Program.BuildContainer(new[] { "help", "run-script", "--extensions=Calamari.Tests" }))
                {
                    container.Resolve<Calamari.Program>();
                }
            }, 1);

            Warn.If(autofacTime > regularTime + IndividualThreshold, 
                $"Expected {autofacTime} to be less than or equal to {regularTime + IndividualThreshold}");
        }

        /// <summary>
        /// Replicate the code that used to be run when executing a command
        /// </summary>
        private void DoOldCalamariExecutionPipeline()
        {
//            // We manually replicate the type lookups that used to happen
//            var runCommand = (ICommand)Activator.CreateInstance(
//                CommandLocator.Find("run-script", typeof(RunScriptCommand).Assembly),
//                new CalamariVariableDictionary(),
//                new CombinedScriptEngine());
//            var helpCommand = (ICommand)Activator.CreateInstance(
//                CommandLocator.Find("help", typeof(HelpCommand).Assembly),
//                CommandLocator.List(typeof(RunScriptCommand).Assembly),
//                runCommand);
//
//            new Calamari.Program(
//                helpCommand,
//                new HelpCommand(Enumerable.Empty<ICommandMetadata>()));
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
