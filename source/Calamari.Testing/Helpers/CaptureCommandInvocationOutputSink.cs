using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.LogParser;
using ServiceMessageParser = Calamari.Common.Plumbing.ServiceMessages.ServiceMessageParser;

namespace Calamari.Testing.Helpers
{
    //Ideally sourced directly from Octopus.Worker repo
    public class CaptureCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        readonly List<string> all = new List<string>();
        readonly List<string> infos = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<ServiceMessage> serviceMessages;
        readonly ServiceMessageParser serviceMessageParser;
        readonly IVariables outputVariables = new CalamariVariables();

        public CaptureCommandInvocationOutputSink()
        {
            serviceMessageParser = new ServiceMessageParser(ParseServiceMessage);
            serviceMessages = new List<ServiceMessage>();
        }

        public void WriteInfo(string line)
        {
            serviceMessageParser.Parse(line);
            all.Add(line);
            infos.Add(line);
        }

        public void WriteError(string line)
        {
            all.Add(line);
            errors.Add(line);
        }

        public IVariables OutputVariables => outputVariables;

        public IList<string> Infos => infos;

        public IList<string> Errors => errors;

        public IList<string> AllMessages => all;

        public IReadOnlyList<ServiceMessage> ServiceMessages => serviceMessages;

        public bool CalamariFoundPackage { get; protected set; }

        public FoundPackage? FoundPackage { get; protected set; }

        public DeltaPackage? DeltaVerification { get; protected set; }

        public string? DeltaError { get; protected set; }

        void ParseServiceMessage(ServiceMessage message)
        {
            serviceMessages.Add(message);
            
            switch (message.Name)
            {
                case ServiceMessageNames.SetVariable.Name:
                    var variableName = message.GetValue(ServiceMessageNames.SetVariable.NameAttribute);
                    var variableValue = message.GetValue(ServiceMessageNames.SetVariable.ValueAttribute);

                    if (!string.IsNullOrWhiteSpace(variableName))
                        outputVariables.Set(variableName, variableValue);
                    break;
                case ServiceMessageNames.CalamariFoundPackage.Name:
                    CalamariFoundPackage = true;
                    break;
                case ServiceMessageNames.FoundPackage.Name:
                    var foundPackageId = message.GetValue(ServiceMessageNames.FoundPackage.IdAttribute);
                    var foundPackageVersion = message.GetValue(ServiceMessageNames.FoundPackage.VersionAttribute);
                    var foundPackageVersionFormat = message.GetValue(ServiceMessageNames.FoundPackage.VersionFormat );
                    var foundPackageHash = message.GetValue(ServiceMessageNames.FoundPackage.HashAttribute);
                    var foundPackageRemotePath = message.GetValue(ServiceMessageNames.FoundPackage.RemotePathAttribute);
                    var fileExtension = message.GetValue(ServiceMessageNames.FoundPackage.FileExtensionAttribute);
                    if (foundPackageId != null && foundPackageVersion != null)
                        FoundPackage = new FoundPackage(foundPackageId,
                                                        foundPackageVersion,
                                                        foundPackageVersionFormat,
                                                        foundPackageRemotePath,
                                                        foundPackageHash,
                                                        fileExtension);
                    break;
                case ServiceMessageNames.PackageDeltaVerification.Name:
                    var pdvHash = message.GetValue(ServiceMessageNames.PackageDeltaVerification.HashAttribute);
                    var pdvSize = message.GetValue(ServiceMessageNames.PackageDeltaVerification.SizeAttribute);
                    var pdvRemotePath =
                        message.GetValue(ServiceMessageNames.PackageDeltaVerification.RemotePathAttribute);
                    DeltaError = message.GetValue(ServiceMessageNames.PackageDeltaVerification.Error);
                    if (pdvRemotePath != null && pdvHash != null)
                    {
                        DeltaVerification = new DeltaPackage(pdvRemotePath, pdvHash, long.Parse(pdvSize));
                    }

                    break;
            }
        }
    }
}