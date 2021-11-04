using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Deployment.Journal
{
    public class DeployedPackage
    {
        public DeployedPackage(string? packageId, string? packageVersion, string? deployedFrom)
        {
            Guard.NotNullOrWhiteSpace(packageId, "Deployed package must have an Id");
            Guard.NotNullOrWhiteSpace(packageVersion, "Deployed package must have a version");
            PackageId = packageId;
            PackageVersion = packageVersion;
            DeployedFrom = deployedFrom;
        }

        public DeployedPackage(XElement element)
            : this(
                element.Attribute("PackageId")?.Value,
                element.Attribute("PackageVersion")?.Value,
                element.Attribute("DeployedFrom")?.Value)
        {
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public string? DeployedFrom { get; }

        public XElement ToXmlElement()
        {
            var attributes = new List<XAttribute>
            {
                new XAttribute("PackageId", PackageId),
                new XAttribute("PackageVersion", PackageVersion)
            };

            if (!string.IsNullOrEmpty(DeployedFrom))
                attributes.Add(new XAttribute("DeployedFrom", DeployedFrom));

            return new XElement("Package", attributes);
        }

        public static IEnumerable<DeployedPackage> GetDeployedPackages(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            foreach (var packageReferenceName in variables.GetIndexes(PackageVariables.PackageCollection))
            {
                yield return new DeployedPackage(
                    variables.Get(PackageVariables.IndexedPackageId(packageReferenceName)),
                    variables.Get(PackageVariables.IndexedPackageVersion(packageReferenceName)),
                    variables.Get(PackageVariables.IndexedOriginalPath(packageReferenceName))
                );
            }
        }

        public static IEnumerable<DeployedPackage> FromJournalEntryElement(XElement deploymentElement)
        {
            // Originally journal entries were restricted to having one package (as deployment steps could 
            // only have one package), and the properties were written as attributes on the <Deployment> element.   
            // When the capability was added for deployment steps to have multiple packages, the package 
            // details were moved to child <Package> elements.
            // We need to support reading both for backwards-compatibility with legacy journal entries.

            // If the deployment element has children, then we will read the packages from there. 
            if (deploymentElement.HasElements)
                return deploymentElement.Elements("Package").Select(x => new DeployedPackage(x));

            // Otherwise we try to read them from the legacy attributes
            var packageIdAttribute = deploymentElement.Attribute("PackageId");
            if (packageIdAttribute != null && !string.IsNullOrEmpty(packageIdAttribute.Value))
                return new[]
                {
                    new DeployedPackage(
                        packageIdAttribute.Value,
                        deploymentElement.Attribute("PackageVersion")?.Value,
                        deploymentElement.Attribute("ExtractedFrom")?.Value)
                };

            return new List<DeployedPackage>();
        }
    }
}