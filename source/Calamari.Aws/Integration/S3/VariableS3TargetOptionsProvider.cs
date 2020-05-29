using System;
using System.Collections.Generic;
using Calamari.Aws.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Calamari.Aws.Integration.S3
{
    public class VariableS3TargetOptionsProvider : IProvideS3TargetOptions
    {
        readonly IVariables variables;


        public VariableS3TargetOptionsProvider(IVariables variables)
        {
            this.variables = variables;
        }

        IEnumerable<S3FileSelectionProperties> GetFileSelections()
        {
            var fileSelectionsAsJson = variables.Get(SpecialVariableNames.Aws.S3.FileSelections);

            return fileSelectionsAsJson == null ? null : Deserialize<List<S3FileSelectionProperties>>(fileSelectionsAsJson);
        }

        S3PackageOptions GetPackageOptions()
        {
            var packageOptionsAsJson = variables.Get(SpecialVariableNames.Aws.S3.PackageOptions);

            return packageOptionsAsJson == null ? null : Deserialize<S3PackageOptions>(packageOptionsAsJson);
        }

        static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            var settings = JsonSerialization.GetDefaultSerializerSettings();

            settings.Converters.Add(new FileSelectionsConverter());
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            return settings;
        }

        static T Deserialize<T>(string value)
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