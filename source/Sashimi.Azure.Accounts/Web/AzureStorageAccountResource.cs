// CS8618 Non-nullable field {0} is uninitialized
// Remove this when this class is converted to initialize all required properties via the constructor

#pragma warning disable 8618

using System;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureStorageAccountResource
    {
        public string Name { get; set; }
        public string Location { get; set; }
    }
}