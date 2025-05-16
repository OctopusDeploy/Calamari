// See https://aka.ms/new-console-template for more information

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Repositories;
using Newtonsoft.Json;
using PackageCacheAnalyzer;

public class Program
{
    public static void Main(string[] args)
    {
        TypeDescriptor.AddAttributes(typeof(ServerTaskId), new TypeConverterAttribute(typeof(TinyTypeTypeConverter<ServerTaskId>)));
        var json = File.ReadAllText("/Users/shanegill/Downloads/PackageRetentionJournal.json");
        var journal = JsonConvert.DeserializeObject<JsonJournalRepository.PackageData>(json);

        var original = new FilteredJournal(journal);
        var scenario = new KeepDaysFilteredJournal(journal, 1);
        var i = 1;
        while (i <= journal.Cache.CacheAge.Value)
        {
            var usage = original.GetEntries(i).SingleOrDefault(entry => entry.GetUsageDetails().Any(u => u.CacheAgeAtUsage.Value == i));
            if (usage == null)
            {
                continue;
            }

            var take1Scenario = scenario.GetEntries(i).Where(entry => entry.Package.PackageId.Value == usage.Package.PackageId.Value);
            var inCache = take1Scenario.Any(entry => entry.Package.Version == usage.Package.Version);
            var originalSize = original.GetEntries(i).Sum(e => (double)e.FileSizeBytes);
            var scenarioSize = scenario.GetEntries(i).Sum(e => (double)e.FileSizeBytes);
            Console.WriteLine($"{i},{usage.Package.PackageId},{inCache},{originalSize},{scenarioSize}");
            
            i++;
        }
    }    
}
