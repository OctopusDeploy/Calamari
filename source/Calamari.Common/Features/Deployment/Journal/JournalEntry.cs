using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Deployment.Journal
{
    public class JournalEntry
    {
        public JournalEntry(RunningDeployment deployment, bool wasSuccessful)
            : this(Guid.NewGuid().ToString(),
                deployment.Variables.Get(DeploymentEnvironment.Id),
                deployment.Variables.Get(DeploymentVariables.Tenant.Id),
                deployment.Variables.Get(ProjectVariables.Id),
                deployment.Variables.Get(KnownVariables.RetentionPolicySet),
                DateTime.UtcNow,
                deployment.Variables.Get(KnownVariables.OriginalPackageDirectoryPath),
                deployment.Variables.Get(PackageVariables.CustomInstallationDirectory),
                wasSuccessful,
                DeployedPackage.GetDeployedPackages(deployment)
            )
        {
        }

        public JournalEntry(XElement element)
            : this(
                element.Attribute("Id")?.Value,
                element.Attribute("EnvironmentId")?.Value,
                element.Attribute("TenantId")?.Value,
                element.Attribute("ProjectId")?.Value,
                element.Attribute("RetentionPolicySet")?.Value,
                ParseDate(element.Attribute("InstalledOn")?.Value),
                element.Attribute("ExtractedTo")?.Value,
                element.Attribute("CustomInstallationDirectory")?.Value,
                ParseBool(element.Attribute("WasSuccessful")?.Value) ?? true,
                DeployedPackage.FromJournalEntryElement(element)
            )
        {
        }

        internal JournalEntry(string? id,
            string? environmentId,
            string? tenantId,
            string? projectId,
            string? retentionPolicySet,
            DateTime? installedOn,
            string? extractedTo,
            string? customInstallationDirectory,
            bool wasSuccessful,
            IEnumerable<DeployedPackage> packages)
        {
            Id = id;
            EnvironmentId = environmentId;
            TenantId = tenantId;
            ProjectId = projectId;
            RetentionPolicySet = retentionPolicySet;
            InstalledOn = installedOn;
            ExtractedTo = extractedTo ?? "";
            CustomInstallationDirectory = customInstallationDirectory;
            WasSuccessful = wasSuccessful;
            Packages = packages?.ToList() ?? new List<DeployedPackage>();
        }

        internal JournalEntry(string id,
            string environmentId,
            string tenantId,
            string projectId,
            string retentionPolicySet,
            DateTime installedOn,
            string extractedTo,
            string customInstallationDirectory,
            bool wasSuccessful,
            DeployedPackage package)
            : this(id,
                environmentId,
                tenantId,
                projectId,
                retentionPolicySet,
                installedOn,
                extractedTo,
                customInstallationDirectory,
                wasSuccessful,
                package != null ? (IEnumerable<DeployedPackage>)new[] { package } : new List<DeployedPackage>())
        {
        }

        public string? Id { get; }
        public string? EnvironmentId { get; }
        public string? TenantId { get; }
        public string? ProjectId { get; }
        public string? RetentionPolicySet { get; }
        public DateTime? InstalledOn { get; }
        public string? ExtractedTo { get; }
        public string? CustomInstallationDirectory { get; }
        public bool WasSuccessful { get; }

        public ICollection<DeployedPackage> Packages { get; }

        // Short-cut for deployments with a single package
        public DeployedPackage Package => Packages.FirstOrDefault();

        public XElement ToXmlElement()
        {
            return new XElement("Deployment",
                new XAttribute("Id", Id),
                new XAttribute("EnvironmentId", EnvironmentId ?? string.Empty),
                new XAttribute("TenantId", TenantId ?? string.Empty),
                new XAttribute("ProjectId", ProjectId ?? string.Empty),
                new XAttribute("InstalledOn", InstalledOn?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                new XAttribute("RetentionPolicySet", RetentionPolicySet ?? string.Empty),
                new XAttribute("ExtractedTo", ExtractedTo ?? string.Empty),
                new XAttribute("CustomInstallationDirectory", CustomInstallationDirectory ?? string.Empty),
                new XAttribute("WasSuccessful", WasSuccessful.ToString()),
                Packages.Select(pkg => pkg.ToXmlElement())
            );
        }

        static DateTime ParseDate(string? s)
        {
            DateTime value;
            if (s != null &&
                (DateTime.TryParseExact(s,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal,
                        out value) ||
                    DateTime.TryParseExact(s,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.CurrentUICulture,
                        DateTimeStyles.AssumeUniversal,
                        out value) ||
                    DateTime.TryParseExact(s,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.CurrentCulture,
                        DateTimeStyles.AssumeUniversal,
                        out value) ||
                    DateTime.TryParseExact(s,
                        "yyyy-MM-dd HH.mm.ss",
                        CultureInfo.CurrentUICulture,
                        DateTimeStyles.AssumeUniversal,
                        out value)))
                return value;

            throw new Exception(string.Format("Could not parse date from '{0}'", s));
        }

        static bool? ParseBool(string? s)
        {
            if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out var b))
                return b;

            return null;
        }
    }
}