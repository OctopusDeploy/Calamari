using System;
using System.Globalization;
using System.Xml.Linq;
using Calamari.Shared;
using Calamari.Shared.Commands;

namespace Calamari.Deployment.Journal
{
    public class JournalEntry
    {
        public JournalEntry(IExecutionContext deployment, bool wasSuccessful)
            : this(Guid.NewGuid().ToString(),
                deployment.Variables.Get(SpecialVariables.Environment.Id),
                deployment.Variables.Get(SpecialVariables.Deployment.Tenant.Id),
                deployment.Variables.Get(SpecialVariables.Project.Id),
                deployment.Variables.Get(SpecialVariables.Package.NuGetPackageId),
                deployment.Variables.Get(SpecialVariables.Package.NuGetPackageVersion),
                deployment.Variables.Get(SpecialVariables.RetentionPolicySet),
                DateTime.UtcNow,
                deployment.PackageFilePath,
                deployment.Variables.Get(SpecialVariables.OriginalPackageDirectoryPath),
                deployment.Variables.Get(SpecialVariables.Package.CustomInstallationDirectory),
                wasSuccessful
            )
        { }
        
        
        public JournalEntry(RunningDeployment deployment, bool wasSuccessful)
            : this(Guid.NewGuid().ToString(),
                deployment.Variables.Get(SpecialVariables.Environment.Id),
                deployment.Variables.Get(SpecialVariables.Deployment.Tenant.Id),
                deployment.Variables.Get(SpecialVariables.Project.Id),
                deployment.Variables.Get(SpecialVariables.Package.NuGetPackageId),
                deployment.Variables.Get(SpecialVariables.Package.NuGetPackageVersion),
                deployment.Variables.Get(SpecialVariables.RetentionPolicySet),
                DateTime.UtcNow,
                deployment.PackageFilePath,
                deployment.Variables.Get(SpecialVariables.OriginalPackageDirectoryPath),
                deployment.Variables.Get(SpecialVariables.Package.CustomInstallationDirectory),
                wasSuccessful
            )
        { }

        public JournalEntry(XElement element)
            : this(
              GetAttribute(element, "Id"),
              GetAttribute(element, "EnvironmentId"),
              GetAttribute(element, "TenantId"),
              GetAttribute(element, "ProjectId"),
              GetAttribute(element, "PackageId"),
              GetAttribute(element, "PackageVersion"),
              GetAttribute(element, "RetentionPolicySet"),
              ParseDate(GetAttribute(element, "InstalledOn")),
              GetAttribute(element, "ExtractedFrom"),
              GetAttribute(element, "ExtractedTo"),
              GetAttribute(element, "CustomInstallationDirectory"),
              ParseBool(GetAttribute(element, "WasSuccessful")) ?? true
           ) 
        { }

        internal JournalEntry(string id, string environmentId, string tenantId, string projectId, string packageId, string packageVersion,
            string retentionPolicySet, DateTime installedOn, string extractedFrom, string extractedTo, string customInstallationDirectory, bool wasSuccessful)
        {
            Id = id;
            EnvironmentId = environmentId;
            TenantId = tenantId;
            ProjectId = projectId;
            PackageId = packageId;
            PackageVersion = packageVersion;
            RetentionPolicySet = retentionPolicySet;
            InstalledOn = installedOn;
            ExtractedFrom = extractedFrom;
            ExtractedTo = extractedTo;
            CustomInstallationDirectory = customInstallationDirectory;
            WasSuccessful = wasSuccessful;
        }

        public string Id { get; private set; }
        public string EnvironmentId { get; private set; }
        public string TenantId { get; private set; }
        public string ProjectId { get; private set; }
        public string PackageId { get; private set; }
        public string PackageVersion { get; private set; }
        public string RetentionPolicySet { get; private set; }
        public DateTime InstalledOn { get; set; }
        public string ExtractedFrom { get; private set; }
        public string ExtractedTo { get; private set; }
        public string CustomInstallationDirectory { get; private set; }
        public bool WasSuccessful { get; private set; }

        public XElement ToXmlElement()
        {
            return new XElement("Deployment",
                new XAttribute("Id", Id),
                new XAttribute("EnvironmentId", EnvironmentId ?? string.Empty),
                new XAttribute("TenantId", TenantId ?? string.Empty),
                new XAttribute("ProjectId", ProjectId ?? string.Empty),
                new XAttribute("PackageId", PackageId ?? string.Empty),
                new XAttribute("PackageVersion", PackageVersion ?? string.Empty),
                new XAttribute("InstalledOn", InstalledOn.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                new XAttribute("ExtractedFrom", ExtractedFrom ?? string.Empty),
                new XAttribute("ExtractedTo", ExtractedTo ?? string.Empty),
                new XAttribute("RetentionPolicySet", RetentionPolicySet ?? string.Empty),
                new XAttribute("CustomInstallationDirectory", CustomInstallationDirectory ?? string.Empty),
                new XAttribute("WasSuccessful", WasSuccessful.ToString()));
        }

        static string GetAttribute(XElement element, string attributeName)
        {
            var attribute = element.Attribute(attributeName);
            return attribute != null ? attribute.Value : null;
        }

        static DateTime ParseDate(string s)
        {
            DateTime value;
            if (s != null &&
                (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value)
                || DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out value)
                || DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out value)
                || DateTime.TryParseExact(s, "yyyy-MM-dd HH.mm.ss", CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal, out value)))
            {
                return value;
            }

            throw new Exception(string.Format("Could not parse date from '{0}'", s));
        }

        static bool? ParseBool(string s)
        {
            bool b;

            if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out b))
            {
                return b;
            }

            return null;
        }
    }
}