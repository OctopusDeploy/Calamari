using System;

namespace Calamari.Common.Features.ConfigurationTransforms
{
    public interface IConfigurationTransformer
    {
        void PerformTransform(string configFile, string transformFile, string destinationFile);
    }
}