using System.Linq;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripting.Bash;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    public class BashPerformanceFixture : CalamariFixture
    {
        [Category("Performance")]
        [TestCase(  100,  1000, Description = "100 variables (~small deployment)")]
        [TestCase(  500,  1200, Description = "500 variables (~medium deployment)")]
        [TestCase(1_000, 1500, Description = "1 000 variables (~large deployment)")]
        [TestCase(5_000, 4500, Description = "5 000 variables (~stress test)")]
        [TestCase(20_000, 14_000, Description = "5 000 variables (~stress test)")]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPopulateOctopusParametersPerformantly(int variableCount, int timeLimitMs)
        {
            // Build the realistic payload separately so we can measure its size
            // before it gets encrypted and written into the bootstrap script.
            var perfVariables = BuildPerformanceTestVariables(variableCount);
            var variables = new Dictionary<string, string>(perfVariables)
                                .AddFeatureToggleToDictionary(new List<FeatureToggle?> { FeatureToggle.BashParametersArrayFeatureToggle });

            var keyBytes   = perfVariables.Keys.Select(k => (long)Encoding.UTF8.GetByteCount(k)).ToArray();
            var valueBytes = perfVariables.Values.Select(v => (long)Encoding.UTF8.GetByteCount(v ?? "")).ToArray();
            var totalKeyBytes   = keyBytes.Sum();
            var totalValueBytes = valueBytes.Sum();
            var totalPairBytes  = totalKeyBytes + totalValueBytes;

            var stopwatch = Stopwatch.StartNew();
            var (output, _) = RunScript("count-octopus-parameters.sh", variables);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // ── Structured performance report ────────────────────────────────────
            static string Kb(long b)    => $"{b / 1024.0,7:F1} KB";
            static string Fmt(double b) => b >= 1024 ? $"{b / 1024.0:F1} KB" : $"{b:F0} B";

            TestContext.WriteLine("");
            TestContext.WriteLine("── Payload ─────────────────────────────────────────────────────");
            TestContext.WriteLine($"  Variables  : {perfVariables.Count,6:N0}");
            TestContext.WriteLine($"  Keys       :  avg {Fmt(keyBytes.Average()),9}  │  min {keyBytes.Min(),5} B  │  max {keyBytes.Max(),7} B  │  total {Kb(totalKeyBytes)}");
            TestContext.WriteLine($"  Values     :  avg {Fmt(valueBytes.Average()),9}  │  min {valueBytes.Min(),5} B  │  max {valueBytes.Max(),7} B  │  total {Kb(totalValueBytes)}");
            TestContext.WriteLine($"  Pairs      :  avg {Fmt(keyBytes.Average() + valueBytes.Average()),9}  │                              │  total {Kb(totalPairBytes)}");
            TestContext.WriteLine("── Timing ──────────────────────────────────────────────────────");
            TestContext.WriteLine($"  Total      : {elapsedMs,7} ms  (limit {timeLimitMs / 1000} s)");
            TestContext.WriteLine($"  Per var    : {elapsedMs / (double)perfVariables.Count,9:F2} ms");
            TestContext.WriteLine($"  Throughput : {totalPairBytes / 1024.0 / (elapsedMs / 1000.0),7:F1} KB/s");
            TestContext.WriteLine("────────────────────────────────────────────────────────────────");

            output.AssertSuccess();

            var fullOutput = string.Join(Environment.NewLine, output.CapturedOutput.Infos);
            if (fullOutput.Contains("Bash version 4.2 or later is required to use octopus_parameters"))
            {
                Assert.Ignore("Bash 4.2+ required for octopus_parameters; performance assertion skipped.");
                return;
            }

            var countLine = output.GetOutputForLineContaining("VariableCount=");
            var loadedCount = int.Parse(Regex.Match(countLine, @"VariableCount=(\d+)").Groups[1].Value);
            loadedCount.Should().BeGreaterThanOrEqualTo(
                variableCount,
                $"octopus_parameters should contain at least the {variableCount} variables we passed in");

            output.AssertOutput("SpotCheck=PerfSentinelValue");

            elapsedMs.Should().BeLessThan(
                timeLimitMs,
                $"Loading {variableCount} variables should complete within {timeLimitMs / 1000.0:F0}s");
        }

        [Category("Performance")]
        [TestCase(20_000, Description = "20 000 variables but array not loaded")]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotLoadOctopusParametersWhenNotUsed(int variableCount)
        {
            // This test verifies that when a script doesn't use octopus_parameters,
            // the encrypted variable string markers are still present in the configuration file
            // (meaning the data is available for get_octopusvariable), but the array
            // is NOT loaded, avoiding the ~6-7s overhead for large variable sets.
            var perfVariables = BuildPerformanceTestVariables(variableCount);
            var variables = new Dictionary<string, string>(perfVariables)
                                .AddFeatureToggleToDictionary(new List<FeatureToggle?> { FeatureToggle.BashParametersArrayFeatureToggle });

            var scriptFile = GetFixtureResource("Scripts", "no-octopus-parameters.sh");
            var workingDirectory = Path.GetDirectoryName(scriptFile);
            
            // Create the configuration file to inspect it
            var script = new Script(scriptFile);
            var calamariVariables = new CalamariVariables();
            foreach (var kvp in variables)
                calamariVariables.Set(kvp.Key, kvp.Value);
            
            var configFile = BashScriptBootstrapper.PrepareConfigurationFile(workingDirectory, 
                calamariVariables, script);
            
            try
            {
                // Read the configuration file content
                var configContent = File.ReadAllText(configFile);
                
                // Verify the encrypted variable string markers ARE still present
                // (because script doesn't use octopus_parameters, the KVP data is NOT included,
                // so the markers should remain unreplaced in the template)
                configContent.Should().Contain("#### VARIABLESTRING.IV ####",
                    "The IV marker should remain unreplaced since the script doesn't use octopus_parameters");
                configContent.Should().Contain("#### VARIABLESTRING.ENCRYPTED ####",
                    "The encrypted marker should remain unreplaced since the script doesn't use octopus_parameters");
                configContent.Should().Contain("scriptUsesOctopusParameters=false",
                    "The script should be marked as NOT using octopus_parameters");
                
                // Now run the script and verify timing
                var stopwatch = Stopwatch.StartNew();
                var (output, _) = RunScript("no-octopus-parameters.sh", variables);
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                // Also measure how long it takes WITH the array loaded for comparison
                var stopwatch2 = Stopwatch.StartNew();
                var (output2, _) = RunScript("count-octopus-parameters.sh", variables);
                stopwatch2.Stop();
                var elapsedWithArrayMs = stopwatch2.ElapsedMilliseconds;
                var savedMs = elapsedWithArrayMs - elapsedMs;

                TestContext.WriteLine("");
                TestContext.WriteLine($"Variables:       {variableCount:N0}");
                TestContext.WriteLine($"Config file size: {new FileInfo(configFile).Length / 1024.0:F1} KB");
                TestContext.WriteLine($"Without array:   {elapsedMs} ms");
                TestContext.WriteLine($"With array:      {elapsedWithArrayMs} ms");
                TestContext.WriteLine($"Time saved:      {savedMs} ms");

                output.AssertSuccess();
                output.AssertOutput("ScriptRan=true");
                output.AssertOutput("SentinelValue=PerfSentinelValue");
            }
            finally
            {
                if (File.Exists(configFile))
                    File.Delete(configFile);
            }
        }

        // ---------------------------------------------------------------------------
        // Realistic variable generator for performance tests.
        //
        // Produces a distribution that approximates a real Octopus deployment:
        //
        //   Bucket  | Share | Name length  | Value length   | Example
        //   --------|-------|--------------|----------------|----------------------------
        //   Small   |  60%  |  15–30 chars |   10–40 chars  | Project.Name, port numbers
        //   Medium  |  25%  |  40–65 chars | 150–300 chars  | SQL / Redis connection strings
        //   Large   |  10%  |  70–130 chars|  700–950 chars | JSON config blobs
        //   Huge    |   5%  | 100–180 chars| 6 000–9 000 chars | PEM certificate + key bundles
        //
        // All keys are unique (every template appends the loop index).
        // A "PerfSentinel" key is added for correctness spot-checks.
        // ---------------------------------------------------------------------------

        static Dictionary<string, string> BuildPerformanceTestVariables(int count)
        {
            var result = new Dictionary<string, string>(count + 1);
            for (var i = 0; i < count; i++)
            {
                var (key, value) = (i % 20) switch
                {
                    < 12 => (PerfSmallKey(i),  PerfSmallValue(i)),
                    < 17 => (PerfMediumKey(i), PerfMediumValue(i)),
                    < 19 => (PerfLargeKey(i),  PerfLargeValue(i)),
                    _    => (PerfHugeKey(i),   PerfHugeValue(i)),
                };
                result[key] = value;
            }
            result["PerfSentinel"] = "PerfSentinelValue";
            return result;
        }

        // Small (60%): names ~15–30 chars, values ~10–40 chars
        static string PerfSmallKey(int i) => (i % 12) switch
        {
            0  => $"Project.Name.{i:D4}",
            1  => $"Environment.Region.{i:D4}",
            2  => $"Application.Version.{i:D4}",
            3  => $"Config.Timeout.Seconds.{i:D4}",
            4  => $"Deploy.Mode.{i:D4}",
            5  => $"Service.Port.{i:D4}",
            6  => $"Feature.{i:D4}.Enabled",
            7  => $"Build.Number.{i:D4}",
            8  => $"Cluster.Zone.{i:D4}",
            9  => $"Agent.Pool.{i:D4}",
            10 => $"Release.Channel.{i:D4}",
            _  => $"Tag.Value.{i:D4}",
        };

        static string PerfSmallValue(int i) => (i % 12) switch
        {
            0  => $"production-{i % 5}",
            1  => $"us-{(i % 2 == 0 ? "east" : "west")}-{i % 3 + 1}",
            2  => $"v{i % 10}.{i % 100 + 1}.{i % 1000}",
            3  => $"{i % 120 + 10}",
            4  => i % 2 == 0 ? "blue-green" : "rolling",
            5  => $"{8080 + i % 100}",
            6  => i % 2 == 0 ? "true" : "false",
            7  => $"build-{i:D6}",
            8  => $"zone-{(char)('a' + i % 3)}",
            9  => $"pool-{i % 4}",
            10 => i % 2 == 0 ? "stable" : "beta",
            _  => $"tag-{i % 20:D3}",
        };

        // Medium (25%): names ~40–65 chars, values ~150–300 chars
        static string PerfMediumKey(int i) => (i % 5) switch
        {
            0 => $"Application.Database.Primary.ConnectionString.{i:D4}",
            1 => $"Azure.Storage.Account{i % 3}.AccessKey.{i:D4}",
            2 => $"Service.Authentication.BearerToken.Endpoint.{i:D4}",
            3 => $"Infrastructure.Cache.Redis{i % 2}.ConnectionString.{i:D4}",
            _ => $"Octopus.Action.Package[MyApp.Service{i % 5}].FeedUri.{i:D4}",
        };

        static string PerfMediumValue(int i) => (i % 5) switch
        {
            0 => $"Server=tcp:sql{i % 3}.database.windows.net,1433;Initial Catalog=AppDb{i % 10};User Id=svc_app;Password=P@ssw0rd{i:D4}!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
            1 => $"DefaultEndpointsProtocol=https;AccountName=storage{i % 5}acct;AccountKey=FAKE-KEY-NOT-A-SECRET-{i:D4};EndpointSuffix=core.windows.net",
            2 => $"https://login.microsoftonline.com/{i:D8}-aaaa-bbbb-cccc-{i:D12}/oauth2/v2.0/token",
            3 => $"cache{i % 2}.redis.cache.windows.net:6380,password=cacheSecret{i:D4}==,ssl=true,abortConnect=false,connectTimeout=5000,syncTimeout=3000",
            _ => $"https://nuget.pkg.github.com/MyOrganisation{i % 3}/index.json?api-version=6.0",
        };

        // Large (10%): names ~70–130 chars, values ~700–950 chars (JSON config blobs)
        static string PerfLargeKey(int i) =>
            $"Octopus.Action[Step {i % 10}: Deploy {(i % 2 == 0 ? "Application" : "Infrastructure")} to {(i % 3 == 0 ? "Production" : "Staging")}].Package[MyCompany.Service{i % 5}].Config.{i:D4}";

        static string PerfLargeValue(int i) => $@"{{
  ""environment"": ""{(i % 2 == 0 ? "production" : "staging")}"",
  ""instanceId"": ""{i:D8}"",
  ""serviceEndpoints"": {{
    ""auth"":   ""https://auth{i % 3}.internal.company.com/oauth/token"",
    ""users"":  ""https://users{i % 3}.internal.company.com/api/v2"",
    ""events"": ""https://events{i % 3}.internal.company.com/stream""
  }},
  ""database"": {{
    ""primary"": ""Server=tcp:primary{i % 2}.db.internal,1433;Initial Catalog=AppDb;User ID=svc_app;Password=P@ssw0rd{i:D6}!;Encrypt=True;Connection Timeout=30;"",
    ""replica"":  ""Server=tcp:replica{i % 3}.db.internal,1433;Initial Catalog=AppDb;User ID=svc_ro;Password=R3adOnly{i:D6}!;Encrypt=True;Connection Timeout=30;""
  }},
  ""cache"": {{
    ""primary"":   ""cache{i % 2}.redis.cache.windows.net:6380,password=cacheP@ss{i:D4}==,ssl=true"",
    ""secondary"": ""cache{(i + 1) % 2}.redis.cache.windows.net:6380,password=cacheP@ss{i:D4}==,ssl=true""
  }},
  ""storage"": {{
    ""accountName"":   ""storage{i % 5}acct"",
    ""containerName"": ""deployments-{i:D4}"",
    ""sasToken"":      ""sv=2021-12-02&ss=b&srt=co&sp=rwdlacupiytfx&se=2025-12-31T23:59:59Z&st=2024-01-01T00:00:00Z&spr=https&sig=fakeSignature{i:D6}==""
  }}
}}";

        // Huge (5%): names ~100–180 chars, values ~6 000–9 000 chars (PEM cert + private key bundle)
        static string PerfHugeKey(int i) =>
            $"Octopus.Action[Step {i % 5}: Long Running {(i % 2 == 0 ? "Database Migration" : "Certificate Rotation")} Task For {(i % 3 == 0 ? "Production-AUS" : "Production-US")} Environment].Package[MyCompany.{(i % 2 == 0 ? "Infrastructure" : "Security")}.Tooling.v{i % 10}].Config.{i:D4}";

        static string PerfHugeValue(int i)
        {
            // Each cert line is 60 chars of base64 + 8-digit serial + "==" + newline ≈ 72 chars.
            // 60 + 52 + 42 lines across 3 PEM blocks ≈ 154 lines × 72 chars ≈ 11 KB per variable.
            const string b64 = "MIIGXTCCBEWgAwIBAgIJAKnmpBuMNbOBMA0GCSqGSIb3DQEBCwUAMIGaMQswCQYD"; // 64 chars
            var sb = new StringBuilder(9000);
            sb.AppendLine($"# Certificate bundle — slot {i % 3}, serial {i:D8}");
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            for (var line = 0; line < 60; line++)
                sb.AppendLine($"{b64.Substring((i + line) % 4, 60)}{i * 31337 + line * 7:D8}==");
            sb.AppendLine("-----END CERTIFICATE-----");
            sb.AppendLine("-----BEGIN FAKE TEST KEY-----");
            for (var line = 0; line < 52; line++)
                sb.AppendLine($"{b64.Substring((i + line + 2) % 4, 60)}{i * 99991 + line * 13:D8}Ag==");
            sb.AppendLine("-----END FAKE TEST KEY-----");
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            for (var line = 0; line < 42; line++)
                sb.AppendLine($"{b64.Substring((i + line + 1) % 4, 60)}{i * 65537 + line * 11:D8}==");
            sb.AppendLine("-----END CERTIFICATE-----");
            return sb.ToString();
        }
    }
}
