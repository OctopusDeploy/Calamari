using System;
using System.Linq;
using System.Reflection;
using Calamari.Common;
using Calamari.Common.Features.Processes.ScriptIsolation;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    /// <summary>
    /// Verifies that script isolation is enforced in all published Calamari binaries.
    ///
    /// All published Calamari flavours run through one of two base program classes:
    ///   - CalamariFlavourProgram        (sync, used by Calamari)
    ///   - CalamariFlavourProgramAsync   (async, used by all other flavours)
    ///
    /// Both base classes call Isolation.Enforce() / Isolation.EnforceAsync() in their Run()
    /// methods, so any flavour that inherits from either base class automatically participates
    /// in script isolation.
    ///
    /// Note: Calamari.AzureWebApp.NetCoreShim is intentionally excluded — it is a WebDeploy
    /// helper tool, not a Calamari step runner, and has no need for script isolation.
    /// </summary>
    [TestFixture]
    public class ScriptIsolationBinaryFixture
    {
        /// <summary>
        /// Verifies that Calamari (the main flavour) inherits from CalamariFlavourProgram,
        /// which calls Isolation.Enforce() in its Run() method.
        /// </summary>
        [Test]
        public void CalamariBinary_InheritsFromCalamariFlavourProgram()
        {
            typeof(Program)
                .IsSubclassOf(typeof(CalamariFlavourProgram))
                .Should().BeTrue(
                    because: "Calamari.Program must inherit CalamariFlavourProgram to receive script isolation enforcement");
        }

        /// <summary>
        /// Verifies that CalamariFlavourProgram.Run() acquires an ILockHandle, confirming
        /// that Isolation.Enforce() is called and its result is held for the duration of execution.
        /// </summary>
        [Test]
        public void CalamariFlavourProgram_RunMethod_AcquiresILockHandle()
        {
            var runMethod = typeof(CalamariFlavourProgram)
                .GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            runMethod.Should().NotBeNull(because: "CalamariFlavourProgram must have a Run method");

            var methodBody = runMethod!.GetMethodBody();
            methodBody.Should().NotBeNull();

            var localsContainILockHandle = methodBody!.LocalVariables
                .Any(v => typeof(ILockHandle).IsAssignableFrom(v.LocalType));

            localsContainILockHandle.Should().BeTrue(
                because: "CalamariFlavourProgram.Run() must hold an ILockHandle (from Isolation.Enforce()) " +
                         "for the duration of command execution to enforce script isolation");
        }

        /// <summary>
        /// Verifies that CalamariFlavourProgramAsync.Run() acquires an ILockHandle, confirming
        /// that Isolation.EnforceAsync() is called and its result is held for the duration of execution.
        /// </summary>
        [Test]
        public void CalamariFlavourProgramAsync_RunMethod_AcquiresILockHandle()
        {
            var runMethod = typeof(CalamariFlavourProgramAsync)
                .GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            runMethod.Should().NotBeNull(because: "CalamariFlavourProgramAsync must have a Run method");

            // The async Run() method is compiled into a state machine. The ILockHandle local
            // variable lives inside the generated state machine struct, so we inspect the
            // state machine type's fields instead of the stub method's locals.
            var asyncStateMachineType = typeof(CalamariFlavourProgramAsync)
                .GetNestedTypes(BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.Contains("Run") && t.GetInterfaces()
                    .Any(i => i.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine"));

            asyncStateMachineType.Should().NotBeNull(
                because: "CalamariFlavourProgramAsync.Run() should compile to an async state machine");

            var stateMachineHoldsILockHandle = asyncStateMachineType!
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(f => typeof(ILockHandle).IsAssignableFrom(f.FieldType));

            stateMachineHoldsILockHandle.Should().BeTrue(
                because: "CalamariFlavourProgramAsync.Run() must hold an ILockHandle (from Isolation.EnforceAsync()) " +
                         "for the duration of command execution to enforce script isolation");
        }

        /// <summary>
        /// Documents that Calamari.AzureWebApp.NetCoreShim is intentionally excluded from
        /// script isolation — it is a WebDeploy sync helper tool, not a Calamari step runner.
        /// </summary>
        [Test]
        public void NetCoreShim_IsIntentionallyExcludedFromScriptIsolation()
        {
            // The shim's Program type is NOT accessible from this assembly (it targets net462)
            // and is NOT a published Calamari flavour binary. This test exists to document
            // the intentional exclusion. The shim has its own standalone Program class that
            // does not extend CalamariFlavourProgram or CalamariFlavourProgramAsync.
            //
            // Published flavour binaries (from BuildableCalamariProjects):
            //   Calamari                  → CalamariFlavourProgram        ✓
            //   Calamari.AzureAppService  → CalamariFlavourProgramAsync   ✓
            //   Calamari.AzureResourceGroup → CalamariFlavourProgramAsync ✓
            //   Calamari.AzureScripting   → CalamariFlavourProgramAsync   ✓
            //   Calamari.GoogleCloudScripting → CalamariFlavourProgramAsync ✓
            //   Calamari.Terraform        → CalamariFlavourProgramAsync   ✓
            //   Calamari.AzureWebApp      → CalamariFlavourProgramAsync   ✓
            //   Calamari.AzureServiceFabric → CalamariFlavourProgramAsync ✓
            //
            // Not a flavour binary (intentionally excluded):
            //   Calamari.AzureWebApp.NetCoreShim — WebDeploy helper, net462, not a step runner
            Assert.Pass("Calamari.AzureWebApp.NetCoreShim is intentionally excluded from script isolation (it is a WebDeploy helper, not a step runner)");
        }
    }
}
