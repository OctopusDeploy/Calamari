using System;

namespace Calamari.Azure.ResourceGroups
{
    interface IResourceGroupTemplateNormalizer
    {
        string Normalize(string json);
    }
}