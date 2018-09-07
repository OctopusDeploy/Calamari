using System.Collections.Generic;
using Calamari.Azure.Deployment.Integration.BlobStorage;
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

        public UploadToBlobStorageOptions(CalamariVariableDictionary variables)
        {
            this.variables = variables;
            FilePaths = variables.Get(AzureSpecialVariables.BlobStorage.FileSelections)
                    ?.Map(Deserialize<List<FileSelectionProperties>>) ?? new List<FileSelectionProperties>();
        }

        public bool UploadEntirePackage =>
            variables.GetEnum(AzureSpecialVariables.BlobStorage.Mode, TargetMode.EntirePackage) ==
            TargetMode.EntirePackage;

        public List<FileSelectionProperties> FilePaths { get; }

        public string ContainerName => variables.Get(AzureSpecialVariables.BlobStorage.ContainerName);

        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x => { x.ContractResolver = new CamelCasePropertyNamesContractResolver(); });
        }

        private static T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, GetEnrichedSerializerSettings());
        }
    }
}