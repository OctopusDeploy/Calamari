﻿using System.IO.Packaging;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Calamari.Azure.CloudServices.Integration.CloudServicePackage.ManifestSchema;
using Calamari.Common.Features.ConfigurationVariables;

namespace Calamari.Azure.CloudServices.Integration.CloudServicePackage
{
    public static class AzureCloudServiceConventions
    {
        public const string RoleLayoutPrefix = "Roles/";
        public const string CtpFormatPackageDefinitionRelationshipType = "http://schemas.microsoft.com/windowsazure/PackageDefinition/Version/2012/03/15";

        public static PackageDefinition ReadPackageManifest(Package package)
        {
            var manifestPart = package.GetPart(
                package.GetRelationshipsByType(CtpFormatPackageDefinitionRelationshipType).Single().TargetUri);

            using (var manifestStream = manifestPart.GetStream())
            using (var xmlReader = XmlReader.Create(manifestStream, XmlUtils.DtdSafeReaderSettings))
            {
               return new PackageDefinition(XDocument.Load(xmlReader).Root);
            }
        }

        public static bool RoleLayoutFilePathIsModifiable(string filePath)
        {
            return filePath.StartsWith("approot\\") || filePath.StartsWith("sitesroot\\");
        }

        public static class PackageFolders
        {
            public const string ServiceDefinition = "ServiceDefinition";
            public const string NamedStreams = "NamedStreams";
            public const string LocalContent = "LocalContent";
        }
    }
}