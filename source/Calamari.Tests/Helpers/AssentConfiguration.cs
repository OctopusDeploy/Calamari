using Assent;
using Assent.Namers;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Helpers
{
    public static class AssentConfiguration
    {
        public static readonly Configuration Default = new Configuration()
                                                       .UsingNamer(TestEnvironment.IsCI
                                                           ? (INamer)new CIAssentNamer()
                                                           : new SubdirectoryNamer("Approved"))
                                                       .SetInteractive(!TestEnvironment.IsCI);

        public static Configuration DefaultWithPostfix(string postfix) => new Configuration()
                                                                          .UsingNamer(TestEnvironment.IsCI
                                                                              ? (INamer)new CIAssentNamer(postfix)
                                                                              : new SubdirectoryNamer("Approved", postfix))
                                                                          .SetInteractive(!TestEnvironment.IsCI);

        public static readonly Configuration Json = new Configuration()
                                                    .UsingNamer(TestEnvironment.IsCI
                                                        ? (INamer)new CIAssentNamer()
                                                        : new SubdirectoryNamer("Approved"))
                                                    .SetInteractive(!TestEnvironment.IsCI)
                                                    .UsingExtension("json");

        public static readonly Configuration Yaml = new Configuration()
                                                    .UsingNamer(TestEnvironment.IsCI
                                                        ? (INamer)new CIAssentNamer()
                                                        : new SubdirectoryNamer("Approved"))
                                                    .SetInteractive(!TestEnvironment.IsCI)
                                                    .UsingExtension("yaml");

        public static readonly Configuration Xml = new Configuration()
                                                   .UsingNamer(TestEnvironment.IsCI
                                                       ? (INamer)new CIAssentNamer()
                                                       : new SubdirectoryNamer("Approved"))
                                                   .SetInteractive(!TestEnvironment.IsCI)
                                                   .UsingExtension("xml");

        public static readonly Configuration Properties = new Configuration()
                                                          .UsingNamer(TestEnvironment.IsCI
                                                              ? (INamer)new CIAssentNamer()
                                                              : new SubdirectoryNamer("Approved"))
                                                          .SetInteractive(!TestEnvironment.IsCI)
                                                          .UsingExtension("properties");
    }
}