using System;
using Assent;

namespace Calamari.Tests.Helpers
{
    public class CIAssentNamer : INamer
    {
        private readonly string postfix;

        public CIAssentNamer(string postfix = null)
        {
            this.postfix = postfix;
        }

        public string GetName(TestMetadata metadata)
        {
            var name = $"{metadata.TestFixture.GetType().Name}.{metadata.TestName}";
            if (postfix != null)
            {
                name += $".{postfix}";
            }
            return CalamariFixture.GetFixtureResource(
                metadata.TestFixture.GetType(),
                "Approved",
                name
            );
        }
    }
}