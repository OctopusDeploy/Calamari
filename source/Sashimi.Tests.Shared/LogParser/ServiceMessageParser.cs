using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Calamari;
using Calamari.Integration.ServiceMessages;
using Octopus.Diagnostics;
using Octopus.Versioning;
using Sashimi.Server.Contracts.ActionHandlers;
using ServiceMessage = Sashimi.Server.Contracts.ActionHandlers.ServiceMessage;

namespace Sashimi.Tests.Shared.LogParser
{
    public class ServiceMessageNameAttribute : Attribute
    {
    }
    
    public class ScriptServiceMessageNames
    {
        public static class SetVariable
        {
            public const string Name = "setVariable";
            public const string NameAttribute = "name";
            public const string ValueAttribute = "value";
            public const string SensitiveAttribute = "sensitive";
        }

        public static class StdOutBehaviour
        {
            public const string Ignore = "stdout-ignore";
            public const string Error = "stdout-error";
            public const string Default = "stdout-default";
            public const string Warning = "stdout-warning";
            public const string Verbose = "stdout-verbose";
            public const string Highlight = "stdout-highlight";
            public const string Wait = "stdout-wait";
        }

        public static class StdErrBehavior
        {
            public const string Ignore = "stderr-ignore";
            public const string Progress = "stderr-progress";
            public const string Error = "stderr-error";
            public const string Default = "stderr-default";
        }

        public static class Progress
        {
            public const string Name = "progress";
            public const string Percentage = "percentage";
            public const string Message = "message";
        }

        public static class CreateArtifact
        {
            public const string Name = "createArtifact";
            public const string PathAttribute = "path";
            public const string NameAttribute = "name";
            public const string LengthAttribute = "length";
        }

        public static class ResultMessage
        {
            public const string Name = "resultMessage";
            public const string MessageAttribute = "message";
        }

        public static class CalamariFoundPackage
        {
            public const string Name = "calamari-found-package";
        }

        public static class FoundPackage
        {
            public const string Name = "foundPackage";
            public const string IdAttribute = "id";
            public const string VersionAttribute = "version";
            public const string VersionFormat = "versionFormat";
            public const string HashAttribute = "hash";
            public const string RemotePathAttribute = "remotePath";
            public const string FileExtensionAttribute = "fileExtension";
        }

        public static class PackageDeltaVerification
        {
            public const string Name = "deltaVerification";
            public const string RemotePathAttribute = "remotePath";
            public const string HashAttribute = "hash";
            public const string SizeAttribute = "size";
            public const string Error = "error";
        }

        public static class ScriptOutputActions
        {
            public const string AccountIdOrNameAttribute = "account";
            public const string CertificateIdOrNameAttribute = "certificate";
            public const string UpdateExistingAttribute = "updateIfExisting";

            public static class CreateAccount
            {
                public const string NameAttribute = "name";
                public const string AccountTypeAttribute = "type";

                public static class CreateTokenAccount
                {
                    [ServiceMessageName] public const string Name = "create-tokenaccount";
                    public const string Token = "token";
                }

                public static class CreateUserPassAccount
                {
                    [ServiceMessageName] public const string Name = "create-userpassaccount";
                    public const string Username = "username";
                    public const string Password = "password";
                }

                public static class CreateAwsAccount
                {
                    [ServiceMessageName] public const string Name = "create-awsaccount";
                    public const string SecretKey = "secretKey";
                    public const string AccessKey = "accessKey";
                }

                public static class CreateAzureAccount
                {
                    [ServiceMessageName] public const string Name = "create-azureaccount";
                    public const string SubscriptionAttribute = "azSubscriptionId";

                    public static class ServicePrincipal
                    {
                        public const string TypeName = "serviceprincipal";
                        public const string ApplicationAttribute = "azApplicationId";
                        public const string TenantAttribute = "azTenantId";
                        public const string PasswordAttribute = "azPassword";
                        public const string EnvironmentAttribute = "azEnvironment";
                        public const string BaseUriAttribute = "azBaseUri";
                        public const string ResourceManagementBaseUriAttribute = "azResourceManagementBaseUri";
                    }
                }
            }

            public static class CreateTarget
            {
                public const string NameAttribute = "name";
                public const string RolesAttribute = "roles";

                public static class CreateKubernetesTarget
                {
                    [ServiceMessageName] public const string Name = "create-kubernetestarget";
                    public const string Namespace = "namespace";
                    public const string ClusterUrl = "clusterUrl";
                    public const string DefaultWorkerPool = "defaultWorkerPool";
                    public const string SkipTlsVerification = "skipTlsVerification";
                    public const string ClusterName = "clusterName";
                    public const string ClusterResourceGroup = "clusterResourceGroup";
                    public const string ClientCertificateIdOrName = "clientCertificate";
                    public const string ServerCertificateIdOrName = "serverCertificate";
                }

                public static class CreateAzureWebAppTarget
                {
                    [ServiceMessageName] public const string Name = "create-azurewebapptarget";
                    public const string WebAppNameAttribute = "webAppName";
                    public const string ResourceGroupNameAttribute = "resourceGroupName";
                    public const string WebAppSlotNameAttribute = "webAppSlot";
                }

                public static class CreateAzureCloudServiceTarget
                {
                    [ServiceMessageName] public const string Name = "create-azurecloudservicetarget";
                    public const string AzureCloudServiceNameAttribute = "azureCloudServiceName";
                    public const string AzureStorageAccountAttribute = "azureStorageAccount";
                    public const string AzureDeploymentSlotAttribute = "azureDeploymentSlot";
                    public const string SwapAttribute = "swap";
                    public const string InstanceCountAttribute = "instanceCount";
                }

                public static class CreateAzureServiceFabricTarget
                {
                    [ServiceMessageName] public const string Name = "create-azureservicefabrictarget";
                    public const string ConnectionEndpointAttribute = "connectionEndpoint";
                    public const string SecurityModeAttribute = "securityMode";
                    public const string CertificateThumbprintAttribute = "certificateThumbprint";
                    public const string ActiveDirectoryUsernameAttribute = "activeDirectoryUsername";
                    public const string ActiveDirectoryPasswordAttribute = "activeDirectoryPassword";
                    public const string CertificateStoreLocationAttribute = "certificateStoreLocation";
                    public const string CertificateStoreNameAttribute = "certificateStoreName";
                }
            }

            public static class DeleteTarget
            {
                [ServiceMessageName] public const string Name = "delete-target";
                public const string MachineIdOrNameAttribute = "machine";
            }
        }
    }
    public class CollectedArtifact
    {
        public CollectedArtifact(string name, string? path)
        {
            this.Name = name;
            this.Path = path;
        }

        public string Name { get; }

        public string? Path { get; }

        public long Length { get; set; }
    }
    
    public class FoundPackage
    {
        public string PackageId { get; }
        public IVersion Version { get; }
        public string? RemotePath { get; }
        public string? Hash { get; }
        public string? FileExtension { get; }

        public FoundPackage(string packageId, string? version, string? versionFormat, string? remotePath, string? hash, string? fileExtension)
        {
            PackageId = packageId;

            if (!Enum.TryParse(versionFormat, out VersionFormat realVersionFormat))
            {
                realVersionFormat = VersionFormat.Semver;
            };

            Version = VersionFactory.CreateVersion(version, realVersionFormat);

            RemotePath = remotePath;
            Hash = hash;
            FileExtension = fileExtension;
        }
    }
    
    public class DeltaPackage
    {
        public DeltaPackage(string fullPathOnRemoteMachine, string hash, long size)
        {
            FullPathOnRemoteMachine = fullPathOnRemoteMachine;
            Hash = hash;
            Size = size;
        }

        public string FullPathOnRemoteMachine { get; }
        public string Hash { get; }
        public long Size { get; }
    }
    
    public class ProcessOutput
    {
        private readonly ProcessOutputSource source;
        private readonly string text;
        private readonly DateTimeOffset occurred;

        public ProcessOutput(ProcessOutputSource source, string text)
            : this(source, text, DateTimeOffset.UtcNow)
        {
        }

        public ProcessOutput(ProcessOutputSource source, string text, DateTimeOffset occurred)
        {
            this.source = source;
            this.text = text;
            this.occurred = occurred;
        }

        public ProcessOutputSource Source
        {
            get
            {
                return this.source;
            }
        }

        public DateTimeOffset Occurred
        {
            get
            {
                return this.occurred;
            }
        }

        public string Text
        {
            get
            {
                return this.text;
            }
        }
    }
    
    public class ScriptOutputFilter
    {
        readonly ILogWithContext log;
        readonly ServiceMessageParser parser;

        readonly Action<string> nullTarget = s =>
        {
        };

        readonly OutputVariableCollection outputVariables = new OutputVariableCollection();
        readonly List<CollectedArtifact> artifacts = new List<CollectedArtifact>();
        readonly List<FoundPackage> foundPackages = new List<FoundPackage>();
        readonly List<ServiceMessage> serviceMessages = new List<ServiceMessage>();
        readonly ILogContext logContext;
        readonly Action<string> debugTarget;
        Action<string> outputTarget;
        Action<string> errorTarget;
        readonly List<ScriptOutputAction> actions = new List<ScriptOutputAction>();
        readonly List<string> supportedScriptActionNames = new List<string>();
        readonly Action<int, string?> progressTarget;

        public ScriptOutputFilter(ILogWithContext log)
        {
            this.log = log;
            DeltaPackageVerifcation = null!;
            parser = new ServiceMessageParser(WritePlainText, ServiceMessage);
            debugTarget = log.Verbose;
            outputTarget = log.Info;
            errorTarget = log.Error;
            logContext = log.CurrentContext;
            PopulateSupportedScriptActionNames();
            progressTarget = log.UpdateProgress;
        }

        /// <summary>
        /// A copy of the collection of service messages that were recorded as part of the
        /// script execution.
        /// </summary>
        public List<ServiceMessage> ServiceMessages => new List<ServiceMessage>(serviceMessages);

        public bool CalamariFoundPackage { get; set; }

        public OutputVariableCollection OutputVariables => outputVariables;

        public List<CollectedArtifact> Artifacts => artifacts;

        public List<FoundPackage> FoundPackages => foundPackages;

        public List<ScriptOutputAction> Actions => actions;

        public DeltaPackage DeltaPackageVerifcation { get; set; }

        public string DeltaPackageError { get; set; } = null!;

        public string ResultMessage { get; private set; } = null!;


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
            using (log.WithinBlock(logContext))
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

                    if (name != null)
                    {
                        outputVariables[name] = new OutputVariable(name, value, isSensitive);

                        if (isSensitive)
                        {
                            // If we're adding a sensitive output-variable we need to add it to the log-context
                            // so it will be masked.
                            logContext.WithSensitiveValue(value);
                        }
                    }

                    break;
                }

                case ScriptServiceMessageNames.Progress.Name:
                {
                    var message = serviceMessage.GetValue(ScriptServiceMessageNames.Progress.Message);
                    if (int.TryParse(serviceMessage.GetValue(ScriptServiceMessageNames.Progress.Percentage), out int percentage))
                        using (log.WithinBlock(logContext))
                            progressTarget(percentage, message);

                    break;
                }

                case ScriptServiceMessageNames.CreateArtifact.Name:
                {
                    var name = serviceMessage.GetValue(ScriptServiceMessageNames.CreateArtifact.NameAttribute);
                    var path = serviceMessage.GetValue(ScriptServiceMessageNames.CreateArtifact.PathAttribute);
                    long.TryParse(serviceMessage.GetValue(ScriptServiceMessageNames.CreateArtifact.LengthAttribute), out long length);

                    if (name != null)
                    {
                        artifacts.Add(new CollectedArtifact(name, path) {Length = length});
                    }

                    break;
                }
                case ScriptServiceMessageNames.ResultMessage.Name:
                    ResultMessage = serviceMessage.GetValue(ScriptServiceMessageNames.ResultMessage.MessageAttribute)!;
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
                    if (id != null)
                    {
                        foundPackages.Add(new FoundPackage(id, version, versionFormat, remotePath, hash, fileExtension));
                    }
                    break;

                case ScriptServiceMessageNames.PackageDeltaVerification.Name:
                    var deltaVerificationRemotePath = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.RemotePathAttribute);
                    var deltaVerificationHash = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.HashAttribute);
                    var deltaVerificationSize = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.SizeAttribute);
                    DeltaPackageError = serviceMessage.GetValue(ScriptServiceMessageNames.PackageDeltaVerification.Error)!;
                    if (deltaVerificationRemotePath != null && deltaVerificationHash != null)
                    {
                        DeltaPackageVerifcation = new DeltaPackage(deltaVerificationRemotePath, deltaVerificationHash, long.Parse(deltaVerificationSize!));
                    }
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
                    {
                        actions.Add(new ScriptOutputAction(serviceMessage.Name, serviceMessage.Properties));
                    }
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
                .Select(x => x.ToString() ?? String.Empty);
            supportedScriptActionNames.AddRange(actionNames);
        }

        IEnumerable<object> GetAllFieldValues(Type t, Func<FieldInfo, bool> filter)
        {
            List<object> values = new List<object>();
            var fields = t.GetFields();
            values.AddRange(fields.Where(x => filter(x)).Select(x => x.GetValue(null)!));

            var nestedTypes = t.GetNestedTypes();
            foreach (var nestedType in nestedTypes)
            {
                values.AddRange(GetAllFieldValues(nestedType, filter));
            }

            return values;
        }
    }
    
    public enum ProcessOutputSource
    {
        StdOut,
        StdErr,
        Debug,
    }
    
    public class ServiceMessageParser
    {
        readonly Action<ProcessOutputSource, string> output;
        readonly Action<ServiceMessage> serviceMessage;
        readonly StringBuilder buffer = new StringBuilder();
        State state = State.Default;
        ProcessOutputSource lastSource;

        public ServiceMessageParser(Action<ProcessOutputSource, string> output, Action<ServiceMessage> serviceMessage)
        {
            this.output = output;
            this.serviceMessage = serviceMessage;
        }

        public void Append(ProcessOutputSource source, string line)
        {
            if (lastSource != source)
                Finish();

            lastSource = source;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                switch (state)
                {
                    case State.Default:
                        if (c == '\r')
                        {
                        }
                        else if (c == '\n')
                        {
                            Flush(output);
                        }
                        else if (c == '#')
                        {
                            state = State.PossibleMessage;
                            buffer.Append(c);
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;

                    case State.PossibleMessage:
                        buffer.Append(c);
                        var progress = buffer.ToString();
                        if ("##octopus" == progress)
                        {
                            state = State.InMessage;
                            buffer.Clear();
                        }
                        else if (!"##octopus".StartsWith(progress))
                        {
                            state = State.Default;
                        }
                        break;

                    case State.InMessage:
                        if (c == ']')
                        {
                            Flush(ProcessMessage);
                            state = State.Default;
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Finish()
        {
            if (buffer.Length > 0)
            {
                Flush(output);
            }
        }

        void ProcessMessage(ProcessOutputSource source, string message)
        {
            try
            {
                message = message.Trim().TrimStart('[').Replace("\r", "").Replace("\n", "");

                var element = XElement.Parse("<" + message + "/>");
                var name = element.Name.LocalName;
                var values = element.Attributes().ToDictionary(s => s.Name.LocalName, s => Encoding.UTF8.GetString(Convert.FromBase64String(s.Value)), StringComparer.OrdinalIgnoreCase);
                serviceMessage(new ServiceMessage(name, values!));
            }
            catch
            {
                serviceMessage(new ServiceMessage("stdout-warning", null));
                output(source, $"Could not parse '##octopus[{message}]'");
                serviceMessage(new ServiceMessage("stdout-default", null));
            }
        }

        void Flush(Action<ProcessOutputSource, string> to)
        {
            var result = buffer.ToString();
            buffer.Clear();

            if (result.Length > 0)
                to(lastSource, result);
        }

        enum State
        {
            Default,
            PossibleMessage,
            InMessage
        }
    }
}