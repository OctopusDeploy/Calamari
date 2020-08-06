using System;

namespace Calamari.AzureResourceGroup
{
    interface IResourceGroupTemplateNormalizer
    {
        string Normalize(string json);
    }
}