using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;

namespace Calamari.Testing.Helpers
{
    /// <summary>
    /// Provides shared infrastructure for script isolation integration tests.
    /// Manages a temporary directory, generates timestamp-writing scripts,
    /// builds <see cref="CommandLine"/> invocations with isolation arguments,
    /// and provides assertions for verifying sequential or concurrent execution.
    /// </summary>
    public sealed class ScriptIsolationTestContext : IDisposable
    {
        string TempDir { get; }

        public ScriptIsolationTestContext()
        {
            TempDir = Path.Combine(Path.GetTempPath(), $"ScriptIsolationIntegration.{Guid.NewGuid()}");
            Directory.CreateDirectory(TempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, recursive: true);
            }
        }

        /// <summary>
        /// Generates a unique mutex name for a test run.
        /// </summary>
        public static string NewMutexName() => $"IntegrationTest.{Guid.NewGuid()}";

        /// <summary>
        /// Configures a <see cref="CommandLine"/> with isolation arguments, variables, and
        /// a per-process working directory. The caller provides a base <see cref="CommandLine"/>
        /// (e.g. from <c>CalamariFixture.Calamari()</c> or <c>new CommandLine(dllPath).UseDotnet()</c>).
        /// </summary>
        public CommandLine BuildInvocation(
            CommandLine baseCommand,
            string processId,
            string mutexName,
            string isolationLevel,
            string timeout = "00:01:00")
        {
            // Each process gets its own working directory so that the inline script
            // file (Script.sh / Script.ps1) written by Calamari doesn't collide
            // between concurrent processes.
            var processWorkDir = Path.Combine(TempDir, $"work-{processId}");
            Directory.CreateDirectory(processWorkDir);

            var variablesFile = Path.Combine(TempDir, $"variables-{processId}.json");
            var variables = new CalamariVariables();

            string scriptBody;
            string syntax;

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                syntax = "PowerShell";
                scriptBody = CreatePowerShellTimestampScript(processId);
            }
            else
            {
                syntax = "Bash";
                scriptBody = CreateBashTimestampScript(processId);
            }

            variables.Set(ScriptVariables.ScriptSource, "Inline");
            variables.Set(ScriptVariables.Syntax, syntax);
            variables.Set(ScriptVariables.ScriptBody, scriptBody);

            var encryptionKey = ((IVariables)variables).SaveAsEncryptedExecutionVariables(variablesFile);

            var envVars = new Dictionary<string, string>
            {
                ["TentacleHome"] = TempDir
            };

            return baseCommand
                .Action("run-script")
                .Argument("variables", variablesFile)
                .Argument("variablesPassword", encryptionKey)
                .Argument("scriptIsolationLevel", isolationLevel)
                .Argument("scriptIsolationMutexName", mutexName)
                .Argument("scriptIsolationTimeout", timeout)
                .WithEnvironmentVariables(envVars)
                .WithWorkingDirectory(processWorkDir);
        }

        /// <summary>
        /// Parses all per-process timestamp log files written by the scripts.
        /// </summary>
        public List<TimestampEntry> ParseTimestampLog()
        {
            var timestampFiles = Directory.GetFiles(TempDir, "timestamps-*.log");
            timestampFiles.Should().NotBeEmpty("at least one timestamp log file should have been created by the scripts");

            var entries = new List<TimestampEntry>();
            foreach (var file in timestampFiles)
            {
                var lines = File.ReadAllLines(file)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                foreach (var line in lines)
                {
                    var parts = line.Split(':');
                    parts.Length.Should().Be(3, $"each timestamp line should have format 'TYPE:PROCESSID:TIMESTAMP', got: '{line}'");

                    entries.Add(new TimestampEntry(
                        Type: parts[0],
                        ProcessId: parts[1],
                        Timestamp: long.Parse(parts[2], CultureInfo.InvariantCulture)
                    ));
                }
            }

            return entries;
        }

        /// <summary>
        /// Asserts that two processes ran sequentially (no time overlap).
        /// </summary>
        public static void AssertSequentialExecution(List<TimestampEntry> entries)
        {
            // We expect exactly 4 entries: START and END for each of two processes
            entries.Should().HaveCount(4, "expected START and END for each of the two processes");

            var processes = entries.GroupBy(e => e.ProcessId).ToList();
            processes.Should().HaveCount(2, "expected entries from exactly two processes");

            var process1 = processes[0].ToList();
            var process2 = processes[1].ToList();

            var p1Start = process1.Single(e => e.Type == "START").Timestamp;
            var p1End = process1.Single(e => e.Type == "END").Timestamp;
            var p2Start = process2.Single(e => e.Type == "START").Timestamp;
            var p2End = process2.Single(e => e.Type == "END").Timestamp;

            // The processes should not overlap. Either P1 finished before P2 started,
            // or P2 finished before P1 started.
            var p1BeforeP2 = p1End <= p2Start;
            var p2BeforeP1 = p2End <= p1Start;

            (p1BeforeP2 || p2BeforeP1).Should().BeTrue(
                $"expected sequential execution but found overlap. " +
                $"Process1: {p1Start}-{p1End}, Process2: {p2Start}-{p2End}");
        }

        /// <summary>
        /// Asserts that both processes wrote their timestamps (for NoIsolation tests).
        /// </summary>
        public static void AssertBothProcessesWroteTimestamps(List<TimestampEntry> entries)
        {
            var processIds = entries.Select(e => e.ProcessId).Distinct().ToList();
            processIds.Should().HaveCount(2, "both processes should have written timestamps");
        }

        string CreateBashTimestampScript(string processId)
        {
            // Each process writes to its own file to avoid concurrent write races.
            // Write START marker, sleep to create a window for overlap detection, write END marker.
            // Uses millisecond-precision timestamps via python or perl fallback.
            // The sleep duration must be long enough that if two processes were running
            // concurrently, their time windows would clearly overlap.
            var perProcessFile = Path.Combine(TempDir, $"timestamps-{processId}.log");
            return $$"""
                     #!/bin/bash
                     TIMESTAMP_FILE="{{EscapeForBash(perProcessFile)}}"
                     get_timestamp() {
                         if command -v python3 &>/dev/null; then
                             python3 -c "import time; print(int(time.time() * 1000))"
                         elif command -v perl &>/dev/null; then
                             perl -MTime::HiRes=time -e "printf('%d', time()*1000)"
                         else
                             date +%s000
                         fi
                     }
                     echo "START:{{processId}}:$(get_timestamp)" >> "$TIMESTAMP_FILE"
                     sleep 2
                     echo "END:{{processId}}:$(get_timestamp)" >> "$TIMESTAMP_FILE"

                     """;
        }

        string CreatePowerShellTimestampScript(string processId)
        {
            var perProcessFile = Path.Combine(TempDir, $"timestamps-{processId}.log");
            return $$"""

                     $timestampFile = '{{EscapeForPowerShell(perProcessFile)}}'
                     function Get-MillisecondTimestamp {
                         [long]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
                     }
                     $startTs = Get-MillisecondTimestamp
                     Add-Content -Path $timestampFile -Value "START:{{processId}}:$startTs"
                     Start-Sleep -Seconds 2
                     $endTs = Get-MillisecondTimestamp
                     Add-Content -Path $timestampFile -Value "END:{{processId}}:$endTs"

                     """;
        }

        static string EscapeForBash(string path)
        {
            return path.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string EscapeForPowerShell(string path)
        {
            return path.Replace("'", "''");
        }

        public sealed record TimestampEntry(string Type, string ProcessId, long Timestamp);
    }
}
