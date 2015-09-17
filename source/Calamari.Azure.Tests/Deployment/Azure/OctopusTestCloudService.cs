using Calamari.Deployment;
using Octostache;

namespace Calamari.Azure.Tests.Deployment.Azure
{
    public class OctopusTestCloudService
    {
        public const string ServiceName = "octopustestapp";
        const string StorageAccountName = "octopusteststorage";

        public static void PopulateVariables(VariableDictionary variables)
        {
            variables.Set(SpecialVariables.Action.Azure.CloudServiceName, ServiceName);
            variables.Set(SpecialVariables.Action.Azure.StorageAccountName, StorageAccountName);
        }
    }
}