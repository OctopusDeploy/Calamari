using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention
{
    public static class JournalEntriesExtensionMethods
    {
        public static bool TryGetFirstValidVersionFormat(this IEnumerable<JournalEntry> journalEntries, out VersionFormat? format)
        {
            format = journalEntries?.FirstOrDefault(e => e.Package?.Version?.Format != null)?.Package?.Version?.Format;

            return format != null;
        }
    }
}