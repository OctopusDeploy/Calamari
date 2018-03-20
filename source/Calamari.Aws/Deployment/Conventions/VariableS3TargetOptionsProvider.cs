using System;
using System.Collections.Generic;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Serialization;
using Calamari.Integration.Processes;
using Calamari.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class VariableS3TargetOptionsProvider : IProvideS3TargetOptions
    {
        private readonly CalamariVariableDictionary variables;


        public VariableS3TargetOptionsProvider(CalamariVariableDictionary variables)
        {
            this.variables = variables;
        }

        private IEnumerable<S3FileSelectionProperties> GetFileSelections()
        {
            return variables.Get(AwsSpecialVariables.S3.FileSelections)
                ?.Map(Deserialize<List<S3FileSelectionProperties>>);
        }

        private S3PackageOptions GetPackageOptions()
        {
            return variables.Get(AwsSpecialVariables.S3.PackageOptions)
                ?.Map(Deserialize<S3PackageOptions>);
        }

        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x =>
                {
                    x.Converters.Add(new FileSelectionsConverter());
                    x.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });
        }

        private static T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, GetEnrichedSerializerSettings());
        }

        public IEnumerable<S3TargetPropertiesBase> GetOptions(S3TargetMode mode)
        {
            switch (mode)
            {
                case S3TargetMode.EntirePackage:
                    return new List<S3TargetPropertiesBase>{GetPackageOptions()};
                case S3TargetMode.FileSelections:
                    return GetFileSelections();
                default:
                    throw new ArgumentOutOfRangeException("Invalid s3 target mode provided", nameof(mode));
            }
        }
    }
}