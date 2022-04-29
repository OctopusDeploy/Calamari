using Assent;
using Assent.Namers;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Helpers
{
    public static class TestEnvironmentExtended
    {
        static bool IsCi => TestEnvironment.IsCI;
        
        public static readonly Configuration AssentConfiguration = new Configuration()
                                                                   .UsingNamer(IsCi ? (INamer) new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                   .SetInteractive(!IsCi);
        
        public static readonly Configuration AssentJsonConfiguration = new Configuration()
                                                                       .UsingNamer(IsCi ? (INamer) new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                       .SetInteractive(!IsCi)
                                                                       .UsingExtension("json");

        public static readonly Configuration AssentYamlConfiguration = new Configuration()
                                                                       .UsingNamer(IsCi ? (INamer)new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                       .SetInteractive(!IsCi)
                                                                       .UsingExtension("yaml");

        public static readonly Configuration AssentXmlConfiguration = new Configuration()
                                                                      .UsingNamer(IsCi ? (INamer)new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                      .SetInteractive(!IsCi)
                                                                      .UsingExtension("xml");

        public static readonly Configuration AssentPropertiesConfiguration = new Configuration()
                                                                             .UsingNamer(IsCi ? (INamer)new CIAssentNamer() : new SubdirectoryNamer("Approved"))
                                                                             .SetInteractive(!IsCi)
                                                                             .UsingExtension("properties");
    }
}