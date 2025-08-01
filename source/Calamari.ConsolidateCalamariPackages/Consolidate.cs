﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class Consolidate
    {
        readonly ILogger log;

        public Consolidate(ILogger log)
        {
            this.log = log;
        }

        public string AssemblyVersion { get; set; } = (((AssemblyInformationalVersionAttribute) typeof(Consolidate).Assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute))!)!).InformationalVersion;

        public (bool result, string packageFileName) Execute(string outputDirectory, IEnumerable<BuildPackageReference> packageReferences)
        {
            using (var hasher = new Hasher())
            {
                if (!Directory.Exists(outputDirectory))
                {
                    log.Error($"The output directory {outputDirectory} does not exist");
                    return (false, null!);
                }

                var packages = GetPackages(hasher, packageReferences);

                var packageHash = hasher.GetPackageCombinationHash(AssemblyVersion, packages);
                log.Information($"Hash of the package combination is {packageHash}");

                var destination = Path.Combine(outputDirectory, $"Calamari.{packageHash}.zip");
                if (File.Exists(destination))
                {
                    log.Information("Calamari zip with the right package combination hash already exists");
                    return (true, destination);
                }

                log.Information("Scanning Calamari Packages");

                var indexEntries = packages.SelectMany(p => p.GetSourceFiles(log)).ToArray();

                log.Information("Creating consolidated Calamari package");
                var sw = Stopwatch.StartNew();

                ConsolidatedPackageCreator.Create(indexEntries, destination);

                log.Information($"Package creation took {sw.ElapsedMilliseconds:n0}ms");

                foreach (var item in indexEntries.Select(i => new {i.PackageId, i.Platform}).Distinct())
                    log.Information($"Packaged {item.PackageId} for {item.Platform}");

                return (true, destination);
            }
        }

        static IReadOnlyList<IPackageReference> GetPackages(Hasher hasher, IEnumerable<BuildPackageReference> packageReferences)
        {
            var calamariPackages = packageReferences
                .Where(p => !CalamariPackages.Flavours.Contains(p.Name))
                .Where(p => p.Name.StartsWith("Calamari"))
                .Select(p => new CalamariPackageReference(hasher, p));
            
            var calamariFlavourPackages = packageReferences
                .Where(p => CalamariPackages.Flavours.Contains(p.Name))
                .Select(p => new CalamariFlavourPackageReference(hasher, p));

            var sashimiPackages = packageReferences
                .Where(p => p.Name.StartsWith("Sashimi."))
                .Select(p => new SashimiPackageReference(hasher, p));

            return calamariPackages.Concat<IPackageReference>(sashimiPackages).Concat(calamariFlavourPackages).ToArray();
        }
    }
}