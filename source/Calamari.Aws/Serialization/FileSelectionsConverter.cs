using System;
using System.Collections.Generic;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.S3;
using Calamari.Serialization;

namespace Calamari.Aws.Serialization
{
    public class FileSelectionsConverter : InheritedClassConverter<S3FileSelectionProperties, S3FileSelectionTypes>
    {
        static readonly IDictionary<S3FileSelectionTypes, Type> SelectionTypeMappings =
            new Dictionary<S3FileSelectionTypes, Type>
            {
                {S3FileSelectionTypes.SingleFile, typeof(S3SingleFileSelectionProperties)},
                {S3FileSelectionTypes.MultipleFiles, typeof(S3MultiFileSelectionProperties)}
            };

        protected override IDictionary<S3FileSelectionTypes, Type> DerivedTypeMappings => SelectionTypeMappings;
        protected override string TypeDesignatingPropertyName { get; } = "Type";
    }
}