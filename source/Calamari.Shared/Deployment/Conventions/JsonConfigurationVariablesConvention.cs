﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;

namespace Calamari.Deployment.Conventions
{
    public class JsonConfigurationVariablesConvention : IInstallConvention
    {
        readonly JsonConfigurationVariablesService service;

        public JsonConfigurationVariablesConvention(JsonConfigurationVariablesService service)
        {
            this.service = service;
        }

        public void Install(RunningDeployment deployment)
        {
            service.Install(deployment);
        }
    }

    public class JsonConfigurationVariablesService
    {
        readonly IJsonConfigurationVariableReplacer jsonConfigurationVariableReplacer;
        readonly ICalamariFileSystem fileSystem;

        public JsonConfigurationVariablesService(IJsonConfigurationVariableReplacer jsonConfigurationVariableReplacer, ICalamariFileSystem fileSystem)
        {
            this.jsonConfigurationVariableReplacer = jsonConfigurationVariableReplacer;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.Package.JsonConfigurationVariablesEnabled))
                return;

            foreach (var target in deployment.Variables.GetPaths(SpecialVariables.Package.JsonConfigurationVariablesTargets))
            {
                if (fileSystem.DirectoryExists(target))
                {
                    Log.Warn($"Skipping JSON variable replacement on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = MatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    Log.Warn($"No files were found that match the replacement target pattern '{target}'");
                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    Log.Info($"Performing JSON variable replacement on '{file}'");
                    jsonConfigurationVariableReplacer.ModifyJsonFile(file, deployment.Variables);
                }
            }
        }

        private List<string> MatchingFiles(RunningDeployment deployment, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, target).Select(Path.GetFullPath).ToList();

            foreach (var path in deployment.Variables.GetStrings(ActionVariables.AdditionalPaths).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}