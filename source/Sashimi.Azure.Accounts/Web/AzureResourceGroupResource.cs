// CS8618 Non-nullable field {0} is uninitialized
// Remove this when this class is converted to initialize all required properties via the constructor

using System;

#pragma warning disable 8618

namespace Sashimi.Azure.Accounts.Web
{
    class AzureResourceGroupResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}