using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Tests.Shared.LogParser;
using Octopus.Diagnostics;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using ServiceMessageParser = Calamari.Tests.Shared.LogParser.ServiceMessageParser;

namespace Sashimi.Tests.Shared.LogParser
{
    public class ScriptOutputFilter : IScriptOutputFilter
    {
        readonly ITaskLog log;
        readonly ServiceMessageParser parser;

        readonly Action<string> nullTarget = s =>
                                             {
                                             };

        readonly List<ServiceMessage> serviceMessages = new();
        readonly Action<string> debugTarget;
        readonly List<string> supportedScriptActionNames = new();
        readonly Action<int, string> progressTarget;
        Action<string> outputTarget;
        Action<string> errorTarget;

        public ScriptOutputFilter(ITaskLog log)
        {
            this.log = log;
            DeltaPackageVerifcation = null;
            DeltaPackageError = null;
            ResultMessage = null;
            parser = new ServiceMessageParser(WritePlainText, ServiceMessage);
            debugTarget = log.Verbose;
            outputTarget = log.Info;
            errorTarget = log.Error;
            PopulateSupportedScriptActionNames();
            progressTarget = log.UpdateProgress;
        }

        /// <summary>
        /// A copy of the collection of service messages that were recorded as part of the
        /// script execution.
        /// </summary>
        public List<ServiceMessage> ServiceMessages => new(serviceMessages);

        public bool CalamariFoundPackage { get; set; }

        public TestOutputVariableCollection TestOutputVariables { get; } = new();

        public List<CollectedArtifact> Artifacts { get; } = new();

        public List<FoundPackage> FoundPackages { get; } = new();

        public List<TestScriptOutputAction> Actions { get; } = new();

        public DeltaPackage? DeltaPackageVerifcation { get; set; }

        public string? DeltaPackageError { get; set; }

        public string? ResultMessage { get; private set; }

        public void Write(IEnumerable<ProcessOutput> output)
        {
            foreach (var line in output)
            {
                parser.Append(line.Source, line.Text);
                parser.Finish();
            }
        }

        public void Write(ProcessOutputSource source, string text)
        {
            parser.Append(source, text);
            parser.Finish();
        }

        void WritePlainText(ProcessOutputSource source, string text)
        {
            switch (source)
            {
                case ProcessOutputSource.Debug:
                    debugTarget(text);
                    break;
                case ProcessOutputSource.StdOut:
                    outputTarget(text);
                    break;
                case ProcessOutputSource.StdErr:
                    errorTarget(text);
                    break;
            }
        }

        void ServiceMessage(ServiceMessage serviceMessage)
        {
            serviceMessages.Add(serviceMessage);

            switch (serviceMessage.Name)
            {
                case ScriptServiceMessageNames.StdErrBehavior.Ignore:
                    errorTarget = nullTarget;
                    break;
                case ScriptServiceMessageNames.StdErrBehavior.Default:
                case ScriptServiceMessageNames.StdErrBehavior.Error:
                    errorTarget = log.Error;
                    break;
                case ScriptServiceMessageNames.StdErrBehavior.Progress:
                    errorTarget = log.Verbose;
                    break;

                case ScriptServiceMessageNames.SetVariable.Name:
                {
                    var name = serviceMessage.GetValue(ScriptServiceMessageNames.SetVariable.NameAttribute);
                    var value = serviceMessage.GetValue(ScriptServiceMessageNames.SetVariable.ValueAttribute);
                    bool.TryParse(serviceMessage.GetValue(ScriptServiceMessageNames.SetVariable.SensitiveAttribute), out var isSensitive);

                    if (name != null && value != null)
                    {
                        TestOutputVariables[name] = new TestOutputVariable(name, value, isSensitive);

                        if (isSensitive)
                            // If we're adding a sensitive output-variable we need to add it to the log-context
                            // so it will be masked.
                            log.WithSensitiveValue(value);
                    }

                    break;
                }

                case ScriptServiceMessageNames.Progress.Name:
                {
                    var message = serviceMessage.GetValue(ScriptServiceMessageNames.Progress.Message);
                    if (message != null && int.TryParse(serviceMessage.GetValue(ScriptServiceMessageNames.Progress.Percentage), out var percentage))
                        progressTarget(percentage, message);

                    break;
                }

                case ScriptServiceMessageNames.CreateArtifact.Name:
                {
                    var name = serviceMessage.GetValue(ScriptServiceMessageNames.CreateArtifact.NameAttribute);
                    var path = serviceMessage.GetValue(ScriptServiceMessageNames.CreateArtifact.PathAttribute);
                    long.TryParse(serviceMessage.GetValue(ScriptServiceMessageNames.CreateArtifact.LengthAttribute), out var length);

                    if (name != null)
                        Artifacts.Add(new CollectedArtifact(name, path) { Length = length });

                    break;
                }
                case ScriptServiceMessageNames.ResultMessage.Name:
                    ResultMessage = serviceMessage.GetValue(ScriptServiceMessageNames.ResultMessage.MessageAttribute);
                    break;

                case ScriptServiceMessageNames.CalamariFoundPackage.Name:
                    CalamariFoundPackage = true;
                    break;

                case ScriptServiceMessageNames.FoundPackage.Name:
                    var id = serviceMessage.GetValue(ScriptServiceMessageNames.FoundPackage.IdAttribute);
                    var version = serviceMessage.GetValue(ScriptServiceMessageNames.FoundPackage.VersionAttribute);
                    var versionFormat = serviceMessage.GetValue(ScriptServiceMessageNames.FoundPackage.VersionFormat);
                    var hash = serviceMessage.GetValue(ScriptServiceMessageNames.FoundPackage.HashAttribute);
                    var remotePath = serviceMessage.GetValue(ScriptServiceMessageNames.FoundPackage.RemotePathAttribute);
                    var fileExtension = serviceMessage.GetValue(ScriptServiceMessageNames.FoundPackage.FileExtensionAttribute);
                    if (id != null && version != null)
                        FoundPackages.Add(new FoundPackage(id,
                                                           version,
                                                           versionFormat,
                                                           remotePath,
                                                           hash,
                                                           fileExtension));
                    break;

                case ScriptServiceMessageNames.PackageDeltaVerification.Name:
                    var deltaVerificationRemotePath = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.RemotePathAttribute);
                    var deltaVerificationHash = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.HashAttribute);
                    var deltaVerificationSize = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.SizeAttribute);
                    DeltaPackageError = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.Error);
                    if (deltaVerificationRemotePath != null && deltaVerificationHash != null && deltaVerificationSize != null)
                        DeltaPackageVerifcation = new DeltaPackage(deltaVerificationRemotePath, deltaVerificationHash, long.Parse(deltaVerificationSize));
                    break;

                case ScriptServiceMessageNames.StdOutBehaviour.Default:
                    outputTarget = log.Info;
                    break;
                case ScriptServiceMessageNames.StdOutBehaviour.Error:
                    outputTarget = log.Error;
                    break;
                case ScriptServiceMessageNames.StdOutBehaviour.Ignore:
                    outputTarget = nullTarget;
                    break;
                case ScriptServiceMessageNames.StdOutBehaviour.Verbose:
                    outputTarget = log.Verbose;
                    break;
                case ScriptServiceMessageNames.StdOutBehaviour.Warning:
                    outputTarget = log.Warn;
                    break;
                case ScriptServiceMessageNames.StdOutBehaviour.Highlight:
                    outputTarget = s => log.Write(LogCategory.Highlight, s);
                    break;
                case ScriptServiceMessageNames.StdOutBehaviour.Wait:
                    outputTarget = s => log.Write(LogCategory.Wait, s);
                    break;
                default:
                    // check to see if it is a support action name
                    if (supportedScriptActionNames.Contains(serviceMessage.Name))
                        Actions.Add(new TestScriptOutputAction(serviceMessage.Name, serviceMessage.Properties));
                    break;
            }
        }

        public void Finish()
        {
            parser.Finish();
        }

        void PopulateSupportedScriptActionNames()
        {
            if (supportedScriptActionNames.Any())
                return;

            var actionNames = GetAllFieldValues(
                                                typeof(ScriptServiceMessageNames.ScriptOutputActions),
                                                x => Attribute.IsDefined(x, typeof(ServiceMessageNameAttribute)))
                .Select(x => x.ToString()!);
            supportedScriptActionNames.AddRange(actionNames);
        }

        static IEnumerable<object> GetAllFieldValues(Type t, Func<FieldInfo, bool> filter)
        {
            var values = new List<object>();
            var fields = t.GetFields();
            values.AddRange(fields.Where(filter).Select(x => x.GetValue(null)!));

            var nestedTypes = t.GetNestedTypes();
            foreach (var nestedType in nestedTypes)
                values.AddRange(GetAllFieldValues(nestedType, filter));

            return values;
        }
    }
}