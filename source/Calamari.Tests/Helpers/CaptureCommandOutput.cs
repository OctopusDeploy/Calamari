using System.Collections.Generic;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octopus.Versioning.Metadata;
using Octostache;

namespace Calamari.Tests.Helpers
{
    public class CaptureCommandOutput : ICommandOutput
    {
        readonly List<string> all = new List<string>();
        readonly List<string> infos = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly ServiceMessageParser serviceMessageParser;
        readonly VariableDictionary outputVariables = new VariableDictionary();

        public CaptureCommandOutput()
        {
            serviceMessageParser = new ServiceMessageParser(ParseServiceMessage);
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

        public VariableDictionary OutputVariables
        {
            get { return outputVariables; }
        }

        public IList<string> Infos
        {
            get { return infos; }
        }

        public IList<string> Errors
        {
            get { return errors; }
        }

        public IList<string> AllMessages
        {
            get { return all; }
        }

        public bool CalamariFoundPackage { get; protected set; }

        public StoredPackage FoundPackage { get; protected set; }

        public StoredPackage DeltaVerification { get; protected set; }

        public string DeltaError { get; protected set; }

        void ParseServiceMessage(ServiceMessage message)
        {
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
                    var foundPackageHash = message.GetValue(ServiceMessageNames.FoundPackage.HashAttribute);
                    var foundPackageRemotePath = message.GetValue(ServiceMessageNames.FoundPackage.RemotePathAttribute);
                    FoundPackage =
                        new StoredPackage(
                            new PhysicalPackageMetadata
                            {
                                PackageId = foundPackageId,
                                Version = foundPackageVersion,
                                Hash = foundPackageHash
                            }, foundPackageRemotePath);
                    break;
                case ServiceMessageNames.PackageDeltaVerification.Name:
                    var pdvHash = message.GetValue(ServiceMessageNames.PackageDeltaVerification.HashAttribute);
                    var pdvSize = message.GetValue(ServiceMessageNames.PackageDeltaVerification.SizeAttribute);
                    var pdvRemotePath = message.GetValue(ServiceMessageNames.PackageDeltaVerification.RemotePathAttribute);
                    DeltaError = message.GetValue(ServiceMessageNames.PackageDeltaVerification.Error);
                    if (pdvHash != null)
                    {
                        DeltaVerification =
                            new StoredPackage(new PhysicalPackageMetadata {Hash = pdvHash},
                                pdvRemotePath);
                    }
                    break;
            }
        }
    }
}