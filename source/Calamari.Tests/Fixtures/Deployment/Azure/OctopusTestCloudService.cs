using Calamari.Deployment;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment.Azure
{
    public class OctopusTestCloudService
    {
        const string CloudServiceName = "octopustestapp";
        const string StorageAccountName = "octopusteststorage";

        public static void PopulateVariables(VariableDictionary variables)
        {
            variables.Set(SpecialVariables.Action.Azure.CloudServiceName, CloudServiceName);
            variables.Set(SpecialVariables.Action.Azure.StorageAccountName, StorageAccountName);
        }
    }
}