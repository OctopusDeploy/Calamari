using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Azure.Deployment
{
    public class UploadToBlobStorageOptions
    {
        private readonly CalamariVariableDictionary variables;

        public UploadToBlobStorageOptions(string containerName, CalamariVariableDictionary variables)
        {
            ContainerName = containerName;
            this.variables = variables;
        }

        public bool UploadPackage => variables.GetFlag(AzureSpecialVariables.BlobStorage.UploadPackage);

        public List<string> Globs => variables.Get(AzureSpecialVariables.BlobStorage.GlobsSelection)?.Map(Deserialize<List<string>>);
        public List<string> FilePaths => variables.Get(AzureSpecialVariables.BlobStorage.FileSelections)?.Map(Deserialize<List<string>>);
        public List<string> SubstitutionPatterns => variables.Get(AzureSpecialVariables.BlobStorage.SubstitutionPatterns)?.Map(Deserialize<List<string>>);
        public string ContainerName { get; }

        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x =>
                {
                    x.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });
        }

        private static T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, GetEnrichedSerializerSettings());
        }
    }
}