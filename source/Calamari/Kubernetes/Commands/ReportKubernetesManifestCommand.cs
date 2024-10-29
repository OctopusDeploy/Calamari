using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Commands
{
    [Command("report-kubernetes-manifest", Description = "Reports a Kubernetes manifest via a service message")]
    public class ReportKubernetesManifestCommand : Command
    {
        string manifestPath;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IManifestReporter manifestReporter;
        string @namespace;

        public ReportKubernetesManifestCommand(ILog log, ICalamariFileSystem fileSystem, IManifestReporter manifestReporter)
        {
            Options.Add("path=", "The path to the manifest file", v => manifestPath = Path.GetFullPath(v));
            Options.Add("namespace=", "The namespace of the manifest (optional)", v => @namespace = v);
            
            this.log = log;
            this.fileSystem = fileSystem;
            this.manifestReporter = manifestReporter;
        }
        
        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            
            if (!fileSystem.FileExists(manifestPath))
                throw new CommandException($"Could not find manifest file: {manifestPath}");

            try
            {
                manifestReporter.ReportManifestApplied(manifestPath, @namespace);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return 1;
            }

            return 0;
        }
    }
}