using System;

namespace Calamari.AzureResourceGroup
{
    public interface IResourceGroupTemplateNormalizer
    {
        string Normalize(string json);
    }
}