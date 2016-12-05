using Calamari.Extensibility.Features;

namespace Calamari.Extensibility.FakeFeatures
{
    [Feature("HelloWorld", "This is me")]
    public class HelloWorldFeature : IFeature
    {
        private readonly ILog logger;
        public HelloWorldFeature(ILog logger)
        {
            this.logger = logger;
        }

        public void Install(IVariableDictionary variables)
        {
            logger.Info("Hello World!");
        }
    }
}
