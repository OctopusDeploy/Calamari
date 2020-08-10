using System;
using System.IO;
using System.Reflection;
using Assent;
using Assent.Namers;
using Calamari.Integration.Processes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Tests.Helpers
{
    public static class TestEnvironment 
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).Assembly.FullLocalPath();
        public static readonly string CurrentWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath);
        public static readonly bool IsCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION"));

        public static readonly Configuration AssentConfiguration = new Configuration()
            .UsingNamer(IsCI ? (INamer) new CIAssentNamer() : new SubdirectoryNamer("Approved"))
            .SetInteractive(!IsCI);
        
        public static readonly Configuration AssentJsonDeepCompareConfiguration = new Configuration()
            .UsingNamer(IsCI ? (INamer) new CIAssentNamer() : new SubdirectoryNamer("Approved"))
            .SetInteractive(!IsCI)
            .UsingExtension("json")
            .UsingComparer((received, approved) =>
            {
                var replacedJson = JToken.Parse(received);
                var expectedJson = JToken.Parse(approved);
                
                if (!JToken.DeepEquals(replacedJson, expectedJson))
                {
                    Console.WriteLine("Expected:");
                    Console.WriteLine(expectedJson.ToString(Formatting.Indented));
            
                    Console.WriteLine("Replaced:");
                    Console.WriteLine(replacedJson.ToString(Formatting.Indented));
                        
                    return CompareResult.Fail("Replaced JSON did not match expected JSON");
                }

                return CompareResult.Pass();
            });

        public static readonly Configuration AssentYamlConfiguration = new Configuration()
                                                                       .UsingNamer(IsCI ? (INamer)new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                       .SetInteractive(!IsCI)
                                                                       .UsingExtension("yaml");

        public static readonly Configuration AssentXmlConfiguration = new Configuration()
            .UsingNamer(IsCI ? (INamer)new CIAssentNamer() : new SubdirectoryNamer("Approved"))
            .SetInteractive(!IsCI)
            .UsingExtension("xml")
            .UsingComparer((received, approved) =>
            {
                var normalisedReceived = received.Replace("\r\n", "\n");
                var normalisedApproved = approved.Replace("\r\n", "\n");

                if (normalisedApproved == normalisedReceived)
                {
                    return CompareResult.Pass();
                }

                Console.WriteLine("Expected:");
                Console.WriteLine(approved);

                Console.WriteLine("Received:");
                Console.WriteLine(received);
                
                return CompareResult.Fail("Received XML did not match approved XML.");
            });

        public static readonly Configuration AssentPropertiesConfiguration = new Configuration()
                                                                             .UsingNamer(IsCI ? (INamer)new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                             .SetInteractive(!IsCI)
                                                                             .UsingExtension("properties");

        public static string GetTestPath(params string[] paths)
        {
            return Path.Combine(CurrentWorkingDirectory, Path.Combine(paths));
        }

        public static string ConstructRootedPath(params string[] paths)
        {
            return Path.Combine(Path.GetPathRoot(CurrentWorkingDirectory), Path.Combine(paths));
        }
    }
}

