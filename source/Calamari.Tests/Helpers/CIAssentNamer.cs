using System;
using Assent;

namespace Calamari.Tests.Helpers
{
    public class CIAssentNamer : INamer
    {
        public string GetName(TestMetadata metadata)
        {
            return CalamariFixture.GetFixtureResource(
                metadata.TestFixture.GetType(),
                "Approved",
                $"{metadata.TestFixture.GetType().Name}.{metadata.TestName}"
            );
        }
    }
}